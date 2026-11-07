using System;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal sealed class AppHost : ApplicationContext
    {
        private readonly Settings _settings;
        private readonly UpdateGuardClient _guard;
        private readonly AppControlWindow _control;
        private OnboardingForm _onboarding;
        private MainForm _main;
        private bool _exiting;

        public AppHost()
        {
            _settings = Settings.Load();
            Localization.Initialize(_settings.Language);
            _guard = new UpdateGuardClient();
            _control = new AppControlWindow(this);
            _control.Show();
            Log.Info("启动 ScreenCurtain，首运行={0}，更新保护={1}，显示器数={2}，数据目录={3}",
                _settings.FirstRun, _settings.GuardEnabled, DisplayManager.Enumerate().Count, AppPaths.DataDirectory);

            if (_settings.FirstRun)
            {
                ShowOnboarding();
            }
            else
            {
                ShowMain();
            }
        }

        public void Activate()
        {
            if (_exiting) return;
            if (_onboarding != null && !_onboarding.IsDisposed)
            {
                if (_onboarding.WindowState == FormWindowState.Minimized) _onboarding.WindowState = FormWindowState.Normal;
                _onboarding.Show();
                _onboarding.Activate();
                return;
            }
            if (_main != null && !_main.IsDisposed)
            {
                _main.ShowMainWindow();
            }
        }

        public void ExitApplication()
        {
            if (_exiting) return;
            _exiting = true;
            Log.Info("应用退出");
            try
            {
                if (_main != null && !_main.IsDisposed)
                {
                    _main.PrepareExit();
                }
            }
            catch (Exception ex)
            {
                Log.Exception("退出前清理主窗口失败", ex);
            }

            try
            {
                _guard.Stop();
            }
            catch (Exception ex)
            {
                Log.Exception("退出时停止更新保护失败", ex);
            }
            try { _guard.Dispose(); } catch { }

            try { if (_main != null && !_main.IsDisposed) _main.Close(); } catch { }
            try { if (_onboarding != null && !_onboarding.IsDisposed) _onboarding.CloseForExit(); } catch { }
            try { _control.Close(); _control.Dispose(); } catch { }
            Log.Flush(1200);
            ExitThread();
        }

        private void ShowOnboarding()
        {
            _onboarding = new OnboardingForm(_settings, _guard);
            _onboarding.Completed += delegate { CompleteOnboarding(); };
            _onboarding.Canceled += delegate
            {
                try { _guard.Stop(); } catch { }
                ExitApplication();
            };
            _onboarding.Show();
        }

        private void CompleteOnboarding()
        {
            _settings.FirstRun = false;
            _settings.Save();
            ShowMain();
            if (_onboarding != null && !_onboarding.IsDisposed)
            {
                _onboarding.CloseForCompletion();
                _onboarding.Dispose();
                _onboarding = null;
            }
        }

        private void ShowMain()
        {
            if (_main != null && !_main.IsDisposed) return;
            _main = new MainForm(_settings, _guard);
            _main.ExitRequested += delegate { ExitApplication(); };
            _main.FormClosed += delegate
            {
                if (!_exiting)
                {
                    _exiting = true;
                    try { _guard.Stop(); } catch { }
                    ExitThread();
                }
            };
            MainForm = _main;
            _main.Show();
            if (_settings.StartMinimized)
            {
                _main.WindowState = FormWindowState.Minimized;
            }
        }
    }
}
