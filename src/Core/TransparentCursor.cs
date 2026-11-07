using System;
using System.IO;
using System.Windows.Forms;

namespace ScreenCurtain
{
    internal static class TransparentCursorFactory
    {
        private static Cursor _cursor;

        public static Cursor Get()
        {
            if (_cursor != null)
            {
                return _cursor;
            }

            string path = null;
            try
            {
                path = Path.Combine(Path.GetTempPath(), "ScreenCurtain-" + Guid.NewGuid().ToString("N") + ".cur");
                File.WriteAllBytes(path, BuildCursorBytes(32));
                _cursor = new Cursor(path);
                Log.Info("已创建透明鼠标指针");
            }
            catch (Exception ex)
            {
                Log.Exception("创建透明鼠标指针失败，将使用普通指针", ex);
                _cursor = Cursors.Default;
            }
            finally
            {
                if (path != null)
                {
                    try { File.Delete(path); }
                    catch { }
                }
            }

            return _cursor;
        }

        private static byte[] BuildCursorBytes(int size)
        {
            int xorBytes = size * size * 4;
            int maskStride = ((size + 31) / 32) * 4;
            int andBytes = maskStride * size;
            int imageBytes = 40 + xorBytes + andBytes;

            byte[] data = new byte[22 + imageBytes];
            int p = 0;

            // ICONDIR
            WriteUInt16(data, ref p, 0);      // reserved
            WriteUInt16(data, ref p, 2);      // type = cursor
            WriteUInt16(data, ref p, 1);      // count

            // ICONDIRENTRY; wPlanes/wBitCount carry hotspot X/Y for cursors.
            data[p++] = (byte)size;
            data[p++] = (byte)size;
            data[p++] = 0;
            data[p++] = 0;
            WriteUInt16(data, ref p, 0);      // hotspot X
            WriteUInt16(data, ref p, 0);      // hotspot Y
            WriteUInt32(data, ref p, (uint)imageBytes);
            WriteUInt32(data, ref p, 22);     // image offset

            // BITMAPINFOHEADER
            WriteUInt32(data, ref p, 40);
            WriteInt32(data, ref p, size);
            WriteInt32(data, ref p, size * 2); // XOR + AND masks
            WriteUInt16(data, ref p, 1);
            WriteUInt16(data, ref p, 32);
            WriteUInt32(data, ref p, 0);       // BI_RGB
            WriteUInt32(data, ref p, (uint)(xorBytes + andBytes));
            WriteInt32(data, ref p, 0);
            WriteInt32(data, ref p, 0);
            WriteUInt32(data, ref p, 0);
            WriteUInt32(data, ref p, 0);

            // XOR pixels: all zero alpha (fully transparent).
            p += xorBytes;

            // AND mask: all 1 bits means "leave screen unchanged".
            for (int i = 0; i < andBytes; i++)
            {
                data[p + i] = 0xFF;
            }

            return data;
        }

        private static void WriteUInt16(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
            buffer[offset++] = (byte)((value >> 16) & 0xFF);
            buffer[offset++] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            WriteUInt32(buffer, ref offset, unchecked((uint)value));
        }
    }
}
