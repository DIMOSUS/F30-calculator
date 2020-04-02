using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AccelerationCalculator
{
    public class Graph
    {
        int H, W;
        double XMax, YMax;
        int XStep, YStep;
        string Name;

        int l_lab_padd = 2;
        int padd = 20;
        int padd_l = 40;
        int x_lb_corn, y_lb_corn;
        int x_rt_corn, y_rt_corn;
        float x_scale, y_scale;

        Bitmap b;
        Graphics g;

        Font f = new Font("Consolas", 10);
        SolidBrush f_br = new SolidBrush(Color.FromArgb(255, 193, 191, 206));
        Pen p1 = new Pen(Color.FromArgb(255, 193, 191, 206), 1);
        Pen p1_g = new Pen(Color.FromArgb(255, 61, 61, 85), 1);
        Pen p2 = new Pen(Color.FromArgb(255, 193, 191, 206), 2);

        Pen p_e = new Pen(Color.FromArgb(255, 255, 212, 88));
        SolidBrush br_e = new SolidBrush(Color.FromArgb(255, 255, 212, 88));

        public Graph(string Name, int W, int H, double XMax = 6, double YMax = 100, int XStep = 1, int YStep = 10)
        {
            this.Name = Name;
            this.H = H; this.W = W;
            this.XMax = XMax; this.YMax = YMax;
            this.XStep = XStep; this.YStep = YStep;

            b = new Bitmap(W, H);
            g = Graphics.FromImage(b);

            padd_l = Math.Max(padd, l_lab_padd * 2 + (int)g.MeasureString(YMax.ToString(), f).Width);
            x_lb_corn = 0 + padd_l;
            y_lb_corn = H - padd - 1;
            x_rt_corn = W - padd - 1;
            y_rt_corn = 0 + padd;

            x_scale = (x_rt_corn - x_lb_corn) / (float)XMax;
            y_scale = (y_lb_corn - y_rt_corn) / (float)YMax;

            g.Clear(Color.FromArgb(255, 27, 30, 54));

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Labels();
        }

        private void Labels()
        {
            for (int y = YStep; y <= YMax; y += YStep)
            {
                string val = y.ToString();
                float textSize = g.MeasureString(val, f).Width;
                float yf = (float)Math.Round(y_lb_corn - y * y_scale);

                g.DrawString(val, f, f_br, x_lb_corn - textSize, yf - 8);
                g.DrawLine(p1_g, x_lb_corn, yf + 0.5f, x_rt_corn, yf + 0.5f);
            }

            for (int x = 0; x <= XMax; x += XStep)
            {
                string val = x.ToString();
                float textSize = g.MeasureString(val, f).Width;
                float xf = (float)Math.Round(x_lb_corn + x * x_scale, 0);

                g.DrawString(val, f, f_br, xf - textSize / 2f + 1, y_lb_corn);
                g.DrawLine(p1_g, xf + 0.5f, y_lb_corn, xf + 0.5f, y_rt_corn);
            }

            g.DrawRectangle(p1_g, 0, 0, W - 1, H - 1);
            g.DrawRectangle(p1, x_lb_corn, y_rt_corn, x_rt_corn - x_lb_corn, y_lb_corn - y_rt_corn);
        }

        public void Point(double X, double Y)
        {
            if (X > XMax || Y > YMax) return;

            float x = x_lb_corn + (float)(X * x_scale);
            float y = y_lb_corn - (float)(Y * y_scale);
            g.FillRectangle(br_e, x, y, 0.5f, 0.5f);
        }

        public void Line(double X1, double Y1, double X2, double Y2)
        {
            if (X1 > XMax || Y1 > YMax || X2 > XMax || Y2 > YMax) return;

            float x1 = x_lb_corn + (float)(X1 * x_scale);
            float y1 = y_lb_corn - (float)(Y1 * y_scale);
            float x2 = x_lb_corn + (float)(X2 * x_scale);
            float y2 = y_lb_corn - (float)(Y2 * y_scale);

            g.DrawLine(p_e, x1, y1, x2, y2);
        }

        public void Save()
        {
            b.Save(Name + ".png", System.Drawing.Imaging.ImageFormat.Png);
            g.Dispose();
            b.Dispose();
        }
    }
}
