using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace JoinNET
{
    class CBmpUtil
    {
        /// <summary>
        /// ファイルから画像データを読み込む
        /// </summary>
        /// <param name="szFile">画像ファイルのパス</param>
        /// <returns>ビットマップ</returns>
        static public BitmapImage LoadImage(string szFile, int nWidth = 0, int nHeight = 0)
        {
            try
            {
                MemoryStream data = new MemoryStream(File.ReadAllBytes(szFile));

                //BitmapSource bmp = BitmapFrame.Create(data);
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = data;
                if (nWidth != 0) bmp.DecodePixelWidth = nWidth;
                if (nHeight != 0) bmp.DecodePixelHeight = nHeight;
                bmp.EndInit();
                bmp.Freeze();

                //data.Dispose();

                return bmp;
            }
            catch
            {
            }

            return null;
        }

        static public BitmapImage LoadImage(byte [] byData, int nWidth = 0, int nHeight = 0)
        {
            try {
                MemoryStream data = new MemoryStream(byData);

                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = data;
                if (nWidth != 0) bmp.DecodePixelWidth = nWidth;
                if (nHeight != 0) bmp.DecodePixelHeight = nHeight;
                bmp.EndInit();
                bmp.Freeze();

                return bmp;
            }

            catch
            {
            }

            return null;
        }

        public enum ImageType
        {
            JPEG = 0,
            PNG,
            BMP,
            TIFF
        }
        static public bool SaveImage(string szFile, BitmapImage bmp, ImageType Type)
        {
            BitmapEncoder enc = null;
            switch (Type)
            {
                case ImageType.BMP:
                    enc = new BmpBitmapEncoder();
                    break;
                case ImageType.PNG:
                    enc = new PngBitmapEncoder();
                    break;
                case ImageType.TIFF:
                    enc = new TiffBitmapEncoder();
                    break;
                case ImageType.JPEG:
                    enc = new JpegBitmapEncoder();
                    break;

            }
            if (enc == null) return false;

            try
            {
                FileStream fs = new FileStream(szFile, FileMode.Create);
                enc.Frames.Add(BitmapFrame.Create(bmp));
                enc.Save(fs);

                fs.Close();
                fs.Dispose();
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 画像をリサイズする
        /// </summary>
        /// <param name="photo"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="scalingMode"></param>
        /// <returns></returns>
        public static BitmapFrame Resize(
            BitmapFrame image, int width, int height,
            BitmapScalingMode scalingMode)
        {
            //DrawingGrouoを作成する
            var group = new DrawingGroup();

            //描画モードを指定する
            RenderOptions.SetBitmapScalingMode(group, scalingMode);

            //画像を設置する
            group.Children.Add(new ImageDrawing(image, new Rect(0, 0, width, height)));

            //描画先を作成する
            var targetVisual = new DrawingVisual();
            var targetContext = targetVisual.RenderOpen();
            targetContext.DrawDrawing(group);
            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Default);
            targetContext.Close();
            
            //描画を行う
            target.Render(targetVisual);
            var targetFrame = BitmapFrame.Create(target);
            return targetFrame;
        }    
    }
}
