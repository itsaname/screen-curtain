using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ScreenCurtain
{
    internal sealed class AudioMuteService
    {
        private readonly object _sync = new object();
        private readonly object _applySync = new object();
        private readonly Dictionary<string, bool> _tracked = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Settings _settings;
        private bool _active;
        private DateTime _nextRefreshUtc;
        private bool _refreshQueued;
        private int _restoreGeneration;
        private Dictionary<string, bool> _pendingRestore;

        public AudioMuteService(Settings settings)
        {
            _settings = settings;
        }

        public bool IsActive
        {
            get { lock (_sync) { return _active; } }
        }

        public void MuteForBlackout()
        {
            lock (_sync)
            {
                if (_active || !_settings.MuteSystemAudio)
                {
                    return;
                }

                _active = true;
                _restoreGeneration++;
                _pendingRestore = null;
                _tracked.Clear();
                DeleteState();
                try
                {
                    List<AudioEndpointState> endpoints = NativeAudio.GetActiveRenderEndpoints();
                    for (int i = 0; i < endpoints.Count; i++)
                    {
                        TrackAndMute(endpoints[i].Id, endpoints[i].Muted);
                    }
                    SaveState();
                    _nextRefreshUtc = DateTime.UtcNow.AddSeconds(2);
                    Log.Info("已静音 {0} 个播放设备", _tracked.Count);
                }
                catch (Exception ex)
                {
                    Log.Exception("静音播放设备失败", ex);
                }
            }
        }

        public void RefreshIfDue()
        {
            lock (_sync)
            {
                RefreshIfDueLocked();
            }
        }

        public void RequestRefresh()
        {
            lock (_sync)
            {
                if (!_active || _refreshQueued || DateTime.UtcNow < _nextRefreshUtc)
                {
                    return;
                }
                _refreshQueued = true;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    lock (_sync)
                    {
                        if (_active)
                        {
                            RefreshIfDueLocked();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception("后台刷新音频静音状态失败", ex);
                }
                finally
                {
                    lock (_sync) { _refreshQueued = false; }
                }
            });
        }

        private void RefreshIfDueLocked()
        {
            if (!_active || DateTime.UtcNow < _nextRefreshUtc)
            {
                return;
            }

            _nextRefreshUtc = DateTime.UtcNow.AddSeconds(2);
            try
            {
                List<AudioEndpointState> endpoints = NativeAudio.GetActiveRenderEndpoints();
                for (int i = 0; i < endpoints.Count; i++)
                {
                    AudioEndpointState endpoint = endpoints[i];
                    bool original;
                    if (!_tracked.TryGetValue(endpoint.Id, out original))
                    {
                        TrackAndMute(endpoint.Id, endpoint.Muted);
                    }
                    else
                    {
                        // Keep all tracked endpoints muted while Dark is active.
                        NativeAudio.SetMute(endpoint.Id, true);
                    }
                }
                SaveState();
            }
            catch (Exception ex)
            {
                Log.Exception("刷新音频静音状态失败", ex);
            }
        }

        public void Restore()
        {
            Dictionary<string, bool> states;
            int generation;
            lock (_sync)
            {
                if (!_active)
                {
                    return;
                }

                states = new Dictionary<string, bool>(_tracked, StringComparer.OrdinalIgnoreCase);
                _active = false;
                _tracked.Clear();
                _pendingRestore = states;
                generation = ++_restoreGeneration;
                _nextRefreshUtc = DateTime.MinValue;
            }

            if (states.Count == 0)
            {
                ClearPending(generation);
                return;
            }

            // Apply once immediately, then retry after the display/audio endpoint
            // has fully resumed. The marker is kept until verification succeeds.
            ApplyRestore(states);
            ScheduleRestoreRetry(states, generation, 500);
            ScheduleRestoreRetry(states, generation, 1500);
            ScheduleRestoreRetry(states, generation, 3000);
        }

        public void RetryPendingRestore()
        {
            Dictionary<string, bool> states;
            int generation;
            lock (_sync)
            {
                if (_active || _pendingRestore == null)
                {
                    return;
                }
                states = new Dictionary<string, bool>(_pendingRestore, StringComparer.OrdinalIgnoreCase);
                generation = _restoreGeneration;
            }

            if (ApplyRestore(states))
            {
                ClearPending(generation);
            }
            else
            {
                ScheduleRestoreRetry(states, generation, 800);
            }
        }

        public static void RecoverStaleState()
        {
            string path = AppPaths.AudioStateFile;
            if (!File.Exists(path)) return;
            try
            {
                Log.Warn("发现上次未恢复的音频状态，开始恢复");
                RestoreStateFile(path);
                Log.Info("已恢复上次异常退出时的音频静音状态");
            }
            catch (Exception ex)
            {
                Log.Exception("恢复遗留音频状态失败", ex);
            }
        }

        public static void EmergencyRestoreFromDisk()
        {
            if (!File.Exists(AppPaths.AudioStateFile)) return;
            Thread thread = new Thread(delegate()
            {
                try
                {
                    RestoreStateFile(AppPaths.AudioStateFile);
                }
                catch (Exception ex)
                {
                    Log.Exception("看门狗恢复音频状态失败", ex);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(1500);
        }

        private void ScheduleRestoreRetry(Dictionary<string, bool> states, int generation, int delayMs)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Thread.Sleep(delayMs);
                    lock (_sync)
                    {
                        if (_restoreGeneration != generation || _active || _pendingRestore == null)
                        {
                            return;
                        }
                    }

                    if (ApplyRestore(states))
                    {
                        ClearPending(generation);
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception("延迟恢复音频状态失败", ex);
                }
            });
        }

        private bool ApplyRestore(Dictionary<string, bool> states)
        {
            bool all = true;
            lock (_applySync)
            {
                foreach (KeyValuePair<string, bool> pair in states)
                {
                    try
                    {
                        bool set = NativeAudio.SetMute(pair.Key, pair.Value);
                        if (!set)
                        {
                            all = false;
                            Log.Warn("恢复音频设备失败：{0}", pair.Key);
                            continue;
                        }

                        bool current;
                        if (NativeAudio.GetMute(pair.Key, out current) && current != pair.Value)
                        {
                            all = false;
                            Log.Warn("音频设备静音状态未生效：{0}", pair.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        all = false;
                        Log.Exception("恢复音频设备异常：" + pair.Key, ex);
                    }
                }
            }

            if (all)
            {
                Log.Info("已恢复 {0} 个播放设备的静音状态", states.Count);
            }
            return all;
        }

        private void ClearPending(int generation)
        {
            lock (_sync)
            {
                if (generation != _restoreGeneration || _active)
                {
                    return;
                }
                _pendingRestore = null;
                DeleteState();
            }
        }

        private void TrackAndMute(string id, bool wasMuted)
        {
            if (string.IsNullOrEmpty(id)) return;
            _tracked[id] = wasMuted;
            NativeAudio.SetMute(id, true);
        }

        private void SaveState()
        {
            if (_tracked.Count == 0)
            {
                DeleteState();
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.AudioStateFile));
                List<string> lines = new List<string>();
                lines.Add("# ScreenCurtain audio restore");
                lines.Add("# base64(device-id)|original-muted");
                foreach (KeyValuePair<string, bool> pair in _tracked)
                {
                    string id64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Key));
                    lines.Add(id64 + "|" + (pair.Value ? "1" : "0"));
                }
                string tmp = AppPaths.AudioStateFile + ".tmp";
                File.WriteAllLines(tmp, lines.ToArray(), new UTF8Encoding(false));
                if (File.Exists(AppPaths.AudioStateFile))
                {
                    try { File.Replace(tmp, AppPaths.AudioStateFile, null); }
                    catch { File.Delete(AppPaths.AudioStateFile); File.Move(tmp, AppPaths.AudioStateFile); }
                }
                else
                {
                    File.Move(tmp, AppPaths.AudioStateFile);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("保存音频恢复状态失败", ex);
            }
        }

        private static void RestoreStateFile(string path)
        {
            if (!NativeAudio.EnsureComInitialized()) return;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                try
                {
                    string id = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(0, bar)));
                    bool original = line.Substring(bar + 1) == "1";
                    NativeAudio.SetMute(id, original);
                }
                catch (Exception ex)
                {
                    Log.Exception("解析音频恢复项失败", ex);
                }
            }
            try { File.Delete(path); } catch { }
        }

        private static void DeleteState()
        {
            try
            {
                if (File.Exists(AppPaths.AudioStateFile))
                {
                    File.Delete(AppPaths.AudioStateFile);
                }
            }
            catch
            {
            }
        }
    }
}
