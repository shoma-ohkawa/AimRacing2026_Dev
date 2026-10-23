/*━━━━━━━━━
@file WindowsNativeImagePrinter.cs
@brief Windows印刷APIを使用して画像を物理プリンタへ直接出力
@author 24cu0312 久場洸太
@date 2026/08/27
最終更新日 2026/09/17
━━━━━━━━━*/

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CrossProjectRanking
{
    public enum WindowsPrintOutputMode
    {
        PhysicalPrinter,
        Pdf
    }

    public enum WindowsPrintPaperSize
    {
        PrinterDefault = 0,
        A4 = 9,
        JapanesePostcard = 43
    }

    /// <summary>
    /// プリンタの用紙設定と画像データをWindows GDIへ直接渡します。
    /// </summary>
    public static class WindowsNativeImagePrinter
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int k_DocumentPropertiesOutput = 2; //プリンタ既定設定を取得するモード
        private const int k_DocumentPropertiesInput = 8; //変更した設定をプリンタDCへ渡すモード
        private const uint k_DeviceModeOrientation = 0x00000001; //用紙方向を指定するフラグ
        private const uint k_DeviceModePaperSize = 0x00000002; //用紙サイズを指定するフラグ
        private const short k_PortraitOrientation = 1; //縦向き印刷
        private const short k_LandscapeOrientation = 2; //横向き印刷
        private const int k_HorizontalResolution = 8; //印刷可能領域の横幅を取得する番号
        private const int k_VerticalResolution = 10; //印刷可能領域の縦幅を取得する番号
        private const int k_HalftoneStretchMode = 4; //縮小時の画質を保つ描画モード
        private const uint k_ColorModeRgb = 0; //DIBの色指定をRGBとして扱う
        private const uint k_CopySourceRasterOperation = 0x00CC0020; //画像をそのまま転送する描画方法
        private const uint k_UncompressedRgb = 0; //無圧縮のRGB画像形式
        private const ushort k_ColorPlaneCount = 1; //DIBが使用するカラープレーン数
        private const ushort k_BitsPerPixel = 32; //プリンタへ渡す1画素あたりのビット数
        private const int k_InitialTextureSize = 2; //PNGを読み込む一時Textureの最小サイズ

        /// <summary>
        /// バックグラウンドスレッドからTextureへアクセスできないため、印刷スプーラ送信に必要な
        /// ピクセル情報だけをメインスレッドで事前に取り出しておくためのデータ。
        /// </summary>
        public readonly struct PreparedPrintImage
        {
            public readonly Color32[] m_pixels;
            public readonly int m_width;
            public readonly int m_height;

            /// <summary>
            /// <param name="_pixels">左下原点のピクセル配列(Texture2D.GetPixels32()と同じ並び)</param>
            /// <param name="_width">画像の幅(px)</param>
            /// <param name="_height">画像の高さ(px)</param>
            /// </summary>
            public PreparedPrintImage(Color32[] _pixels, int _width, int _height)
            {
                m_pixels = _pixels;
                m_width = _width;
                m_height = _height;
            }
        }

        /// <summary>
        /// PNGを読み込み、印刷スプーラ送信用のピクセル情報を取り出します。TextureはメインスレッドでのみAPIを呼べるため、
        /// 実際のプリンタ送信(PrintPixels)より前に必ずメインスレッドで呼び出してください。
        /// </summary>
        public static PreparedPrintImage PrepareImage(string _imagepath)
        {
            if (string.IsNullOrWhiteSpace(_imagepath) || !File.Exists(_imagepath))
            {
                throw new FileNotFoundException("印刷する画像が見つかりません。", _imagepath);
            }

            Texture2D texture = LoadTexture(_imagepath);
            try
            {
                return new PreparedPrintImage(texture.GetPixels32(), texture.width, texture.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// 事前に取り出したピクセル情報を、指定用紙へ収まる大きさで中央配置してプリンタへ直接出力します。
        /// Unity APIに依存しないため、バックグラウンドスレッドから呼び出せます(プリンタ応答待ちで数秒ブロックすることがあるため)。
        /// </summary>
        public static void Print(
            PreparedPrintImage _image,
            string _printername,
            WindowsPrintPaperSize _papersize,
            string _outputpath)
        {
            if (string.IsNullOrWhiteSpace(_printername))
            {
                throw new ArgumentException("プリンタ名が設定されていません。", nameof(_printername));
            }

            PrintPixels(
                _image,
                _printername,
                _papersize,
                _outputpath);
        }

        /// <summary>PNGを印刷用の読取り可能なTextureへ変換します。</summary>
        private static Texture2D LoadTexture(string _imagepath)
        {
            byte[] imageBytes = File.ReadAllBytes(_imagepath);
            Texture2D texture = new Texture2D(
                k_InitialTextureSize,
                k_InitialTextureSize,
                TextureFormat.RGBA32,
                false);
            if (!ImageConversion.LoadImage(texture, imageBytes, false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidDataException("印刷画像をPNGとして読み込めませんでした。");
            }
            return texture;
        }

        /// <summary>プリンタの用紙設定を取得し、画像の向きと指定用紙を反映して印刷します。</summary>
        private static void PrintPixels(
            PreparedPrintImage _image,
            string _printername,
            WindowsPrintPaperSize _papersize,
            string _outputpath)
        {
            IntPtr printerHandle;
            if (!OpenPrinter(_printername, out printerHandle, IntPtr.Zero))
            {
                ThrowLastWindowsError("プリンタを開けませんでした");
            }

            IntPtr deviceMode = IntPtr.Zero;
            IntPtr deviceContext = IntPtr.Zero;
            try
            {
                deviceMode = CreateDeviceMode(
                    printerHandle,
                    _printername,
                    _papersize,
                    _image.m_width > _image.m_height);
                deviceContext = CreateDC("WINSPOOL", _printername, null, deviceMode);
                if (deviceContext == IntPtr.Zero)
                {
                    ThrowLastWindowsError("プリンタ描画領域を作成できませんでした");
                }
                PrintToDeviceContext(
                    deviceContext,
                    _image,
                    _outputpath);
            }
            finally
            {
                if (deviceContext != IntPtr.Zero)
                {
                    DeleteDC(deviceContext);
                }
                if (deviceMode != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(deviceMode);
                }
                ClosePrinter(printerHandle);
            }
        }

        /// <summary>プリンタ既定値を基に、はがき等の用紙サイズと画像方向を設定します。</summary>
        private static IntPtr CreateDeviceMode(
            IntPtr _printerhandle,
            string _printername,
            WindowsPrintPaperSize _papersize,
            bool _islandscape)
        {
            int deviceModeSize = DocumentProperties(
                IntPtr.Zero,
                _printerhandle,
                _printername,
                IntPtr.Zero,
                IntPtr.Zero,
                0);
            if (deviceModeSize <= 0)
            {
                ThrowLastWindowsError("プリンタ設定を取得できませんでした");
            }

            IntPtr deviceModePointer = Marshal.AllocHGlobal(deviceModeSize);
            int result = DocumentProperties(
                IntPtr.Zero,
                _printerhandle,
                _printername,
                deviceModePointer,
                IntPtr.Zero,
                k_DocumentPropertiesOutput);
            if (result < 0)
            {
                Marshal.FreeHGlobal(deviceModePointer);
                ThrowLastWindowsError("プリンタ既定設定を読み込めませんでした");
            }

            DeviceMode deviceMode = Marshal.PtrToStructure<DeviceMode>(deviceModePointer);
            deviceMode.m_fields |= k_DeviceModeOrientation;
            if (_islandscape)
            {
                deviceMode.m_orientation = k_LandscapeOrientation;
            }
            else
            {
                deviceMode.m_orientation = k_PortraitOrientation;
            }
            if (_papersize != WindowsPrintPaperSize.PrinterDefault)
            {
                deviceMode.m_fields |= k_DeviceModePaperSize;
                deviceMode.m_paperSize = (short)_papersize;
            }
            Marshal.StructureToPtr(deviceMode, deviceModePointer, false);

            result = DocumentProperties(
                IntPtr.Zero,
                _printerhandle,
                _printername,
                deviceModePointer,
                deviceModePointer,
                k_DocumentPropertiesOutput | k_DocumentPropertiesInput);
            if (result < 0)
            {
                Marshal.FreeHGlobal(deviceModePointer);
                ThrowLastWindowsError("用紙設定をプリンタへ適用できませんでした");
            }
            return deviceModePointer;
        }

        /// <summary>印刷可能領域へ画像を比率維持で収め、中央へ描画します。</summary>
        private static void PrintToDeviceContext(
            IntPtr _devicecontext,
            PreparedPrintImage _image,
            string _outputpath)
        {
            DocumentInfo documentInfo = new DocumentInfo
            {
                m_size = Marshal.SizeOf<DocumentInfo>(),
                m_documentName = "AimRacing Result",
                m_output = _outputpath,
                m_dataType = null,
                m_type = 0
            };
            if (StartDoc(_devicecontext, ref documentInfo) <= 0)
            {
                ThrowLastWindowsError("印刷ドキュメントを開始できませんでした");
            }

            bool documentCompleted = false;
            try
            {
                if (StartPage(_devicecontext) <= 0)
                {
                    ThrowLastWindowsError("印刷ページを開始できませんでした");
                }

                DrawTexture(_devicecontext, _image);
                if (EndPage(_devicecontext) <= 0)
                {
                    ThrowLastWindowsError("印刷ページを確定できませんでした");
                }
                if (EndDoc(_devicecontext) <= 0)
                {
                    ThrowLastWindowsError("印刷ドキュメントを確定できませんでした");
                }
                documentCompleted = true;
            }
            finally
            {
                if (!documentCompleted)
                {
                    AbortDoc(_devicecontext);
                }
            }
        }

        /// <summary>UnityのRGBA画像をWindows DIBへ変換し、印刷可能領域の中央へ転送します。</summary>
        private static void DrawTexture(IntPtr _devicecontext, PreparedPrintImage _image)
        {
            int pageWidth = GetDeviceCaps(_devicecontext, k_HorizontalResolution);
            int pageHeight = GetDeviceCaps(_devicecontext, k_VerticalResolution);
            float scale = Mathf.Min(
                (float)pageWidth / _image.m_width,
                (float)pageHeight / _image.m_height);
            int outputWidth = Mathf.RoundToInt(_image.m_width * scale);
            int outputHeight = Mathf.RoundToInt(_image.m_height * scale);
            int outputX = (pageWidth - outputWidth) / 2;
            int outputY = (pageHeight - outputHeight) / 2;

            Color32[] sourcePixels = _image.m_pixels;
            byte[] dibPixels = new byte[sourcePixels.Length * 4];
            for (int i = 0; i < sourcePixels.Length; ++i)
            {
                int byteIndex = i * 4;
                dibPixels[byteIndex] = sourcePixels[i].b;
                dibPixels[byteIndex + 1] = sourcePixels[i].g;
                dibPixels[byteIndex + 2] = sourcePixels[i].r;
                dibPixels[byteIndex + 3] = sourcePixels[i].a;
            }

            BitmapInfo bitmapInfo = new BitmapInfo
            {
                m_header = new BitmapInfoHeader
                {
                    m_size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    m_width = _image.m_width,
                    m_height = _image.m_height,
                    m_planes = k_ColorPlaneCount,
                    m_bitCount = k_BitsPerPixel,
                    m_compression = k_UncompressedRgb
                }
            };
            SetStretchBltMode(_devicecontext, k_HalftoneStretchMode);
            SetBrushOrgEx(_devicecontext, 0, 0, IntPtr.Zero);
            int copiedLines = StretchDIBits(
                _devicecontext,
                outputX,
                outputY,
                outputWidth,
                outputHeight,
                0,
                0,
                _image.m_width,
                _image.m_height,
                dibPixels,
                ref bitmapInfo,
                k_ColorModeRgb,
                k_CopySourceRasterOperation);
            if (copiedLines == 0)
            {
                ThrowLastWindowsError("画像を印刷ページへ描画できませんでした");
            }
        }

        /// <summary>Windows APIの直近エラーを、印刷工程が分かる例外として通知します。</summary>
        private static void ThrowLastWindowsError(string _message)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), _message);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DocumentInfo
        {
            public int m_size;
            [MarshalAs(UnmanagedType.LPWStr)] public string m_documentName;
            [MarshalAs(UnmanagedType.LPWStr)] public string m_output;
            [MarshalAs(UnmanagedType.LPWStr)] public string m_dataType;
            public uint m_type;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DeviceMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string m_deviceName;
            public ushort m_specVersion;
            public ushort m_driverVersion;
            public ushort m_size;
            public ushort m_driverExtra;
            public uint m_fields;
            public short m_orientation;
            public short m_paperSize;
            public short m_paperLength;
            public short m_paperWidth;
            public short m_scale;
            public short m_copies;
            public short m_defaultSource;
            public short m_printQuality;
            public short m_color;
            public short m_duplex;
            public short m_yResolution;
            public short m_trueTypeOption;
            public short m_collate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string m_formName;
            public ushort m_logPixels;
            public uint m_bitsPerPixel;
            public uint m_pixelsWidth;
            public uint m_pixelsHeight;
            public uint m_displayFlags;
            public uint m_displayFrequency;
            public uint m_icmMethod;
            public uint m_icmIntent;
            public uint m_mediaType;
            public uint m_ditherType;
            public uint m_reserved1;
            public uint m_reserved2;
            public uint m_panningWidth;
            public uint m_panningHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint m_size;
            public int m_width;
            public int m_height;
            public ushort m_planes;
            public ushort m_bitCount;
            public uint m_compression;
            public uint m_sizeImage;
            public int m_xPixelsPerMeter;
            public int m_yPixelsPerMeter;
            public uint m_colorsUsed;
            public uint m_colorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RgbQuad
        {
            public byte m_blue;
            public byte m_green;
            public byte m_red;
            public byte m_reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader m_header;
            public RgbQuad m_colors;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(
            string _printername,
            out IntPtr _printerhandle,
            IntPtr _printerdefaults);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr _printerhandle);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DocumentProperties(
            IntPtr _windowhandle,
            IntPtr _printerhandle,
            string _devicename,
            IntPtr _devicemodeoutput,
            IntPtr _devicemodeinput,
            int _mode);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDC(
            string _driver,
            string _device,
            string _output,
            IntPtr _initializationdata);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr _devicecontext);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int StartDoc(IntPtr _devicecontext, ref DocumentInfo _documentinfo);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int StartPage(IntPtr _devicecontext);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int EndPage(IntPtr _devicecontext);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int EndDoc(IntPtr _devicecontext);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int AbortDoc(IntPtr _devicecontext);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr _devicecontext, int _index);

        [DllImport("gdi32.dll")]
        private static extern int SetStretchBltMode(IntPtr _devicecontext, int _mode);

        [DllImport("gdi32.dll")]
        private static extern bool SetBrushOrgEx(
            IntPtr _devicecontext,
            int _x,
            int _y,
            IntPtr _previouspoint);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int StretchDIBits(
            IntPtr _devicecontext,
            int _destinationx,
            int _destinationy,
            int _destinationwidth,
            int _destinationheight,
            int _sourcex,
            int _sourcey,
            int _sourcewidth,
            int _sourceheight,
            byte[] _bits,
            ref BitmapInfo _bitmapinfo,
            uint _usage,
            uint _rasteroperation);
#else
        /// <summary>Windows以外では物理プリンタへ直接出力できないことを通知します。</summary>
        public readonly struct PreparedPrintImage
        {
        }

        /// <summary>Windows以外では物理プリンタへ直接出力できないことを通知します。</summary>
        public static PreparedPrintImage PrepareImage(string _imagepath)
        {
            throw new PlatformNotSupportedException("Windows印刷APIはWindowsでのみ利用できます。");
        }

        /// <summary>Windows以外では物理プリンタへ直接出力できないことを通知します。</summary>
        public static void Print(
            PreparedPrintImage _image,
            string _printername,
            WindowsPrintPaperSize _papersize,
            string _outputpath)
        {
            throw new PlatformNotSupportedException("Windows印刷APIはWindowsでのみ利用できます。");
        }
#endif
    }
}
