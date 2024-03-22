using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

#pragma warning disable CA1416

unsafe
{
    var bitmap = new Bitmap("D:\\Coding\\Jupiter\\FeijaoRip\\CroppedAndRemoved.png");
    
    var stride = 0;
    var width = bitmap.Width;
    var height = bitmap.Height;
    var imageData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
    if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
        stride = imageData.Stride / 4;
    else
        stride = imageData.Stride / 3;


    var alphaThreshold = 200;

    var curve1ScaleYPixel = 450;

    const int xaxis = 455;
    const int yaxis = 40;
    
    var ptr = (uint*)imageData.Scan0.ToPointer();

    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var pixelIndex = y * stride + x;
            var pixel = *(ptr + pixelIndex);

            byte a = 255;
            if (bitmap.PixelFormat != PixelFormat.Format24bppRgb)
                a = (byte)((byte)((pixel & 0xFF000000) >> 24) > alphaThreshold ? 255 : 0);

            *(ptr + pixelIndex) = (uint)(a << 24) | (pixel & 0x00FFFFFF);
        }
    }
    
    (int pixel, double value) firstXScale = (0, 0);
    (int pixel, double value) lastXScale = (0, 0);
    
    (int pixel, double value) firstYScale = (xaxis, 0.01d);
    (int pixel, double value) lastYScale = (10, 1d);

    var power = 0.01d;
    var scaleCnt = 1;
    var isInScaleBar = false;
    for (var x = yaxis; x < width; x++)
    {
        var xScalePixel = *(ptr + (curve1ScaleYPixel * stride) + x);

        if ((xScalePixel & 0xFF000000) > 0) isInScaleBar = true;
        else if (isInScaleBar)
        {
            isInScaleBar = false;
            var v = (scaleCnt++) * power;
            if (firstXScale == (0, 0)) firstXScale = (x, v);
            lastXScale = (x, v);
        }

        if (scaleCnt == 10)
        {
            power *= 10;
            scaleCnt = 1;
        }
    }

    var offsets = new int[] { -1, 0, 1 };
    
    /*for (var y = 4; y < xaxis; y++)
    {
        for (var x = yaxis; x < width - 2; x++)
        {
            var centerPixel = *(ptr + (y * stride + x));
            
            var redNeighbors = 0;
            var greenNeighbors = 0;
            var blueNeighbors = 0;
            foreach (var yOff in offsets)
            {
                foreach (var xOff in offsets)
                {
                    var pixelIndex = (y + yOff) * stride + (x + xOff);
                    var pixel = *(ptr + pixelIndex);
                    
                    if ((pixel & 0xFF000000) == 0) continue;
                    
                    if ((pixel & 0x00FF0000) != 0) redNeighbors++;
                    if ((pixel & 0x0000FF00) != 0) greenNeighbors++;
                    if ((pixel & 0x000000FF) != 0) blueNeighbors++;

                    if (redNeighbors > 5)
                        *(ptr + pixelIndex) |= (uint)0xFFFF0000;
                    else if (greenNeighbors > 5)
                        *(ptr + pixelIndex) |= (uint)0xFF00FF00;
                    else if (blueNeighbors > 5)
                        *(ptr + pixelIndex) |= (uint)0xFF0000FF;
                }
            }
        }
    }*/

    var points = new (double p, double x, double r, double g, double b)[2*(width-yaxis)];
    
    for (var x = yaxis; x < width; x++)
    {
        var xValue = GetValueInLogScale(x - yaxis, firstXScale, lastXScale);

        var dx = x - yaxis;

        double minRed = double.MaxValue, maxRed = double.MinValue;
        double minGreen = double.MaxValue, maxGreen = double.MinValue;
        double minBlue = double.MaxValue, maxBlue = double.MinValue;
        int firstRed = 0, firstGreen = 0, firstBlue = 1;
        bool inRed = false, inGreen = false, inBlue = false;
        for (var y = xaxis; y > 0; y--)
        {
            var pixel = *(ptr + (y * stride) + x);
         
            //if ((pixel & 0xFF000000) == 0) continue;
            
            //R
            if ((pixel & 0x00FF0000) >> 16 != 0 && (pixel & 0xFF000000) != 0)
            {
                if (!inRed) firstRed = y;
                inRed = true;
            } 
            else if (inRed)
            {
                inRed = false;
                var v = GetValueInLogScale(((firstRed + y) / 2) - xaxis, firstYScale, lastYScale);
                if (v > maxRed) maxRed = v;
                if (v < minRed) minRed = v;

                *(ptr + (((firstRed + y) / 2) * stride) + x) = (uint)0xff00ff96;
            }

            //G
            if ((pixel & 0x0000FF00) >> 8 != 0 && (pixel & 0xFF000000) != 0)
            {
                if (!inGreen) firstGreen = y;
                inGreen = true;
            } 
            else if (inGreen)
            {
                inGreen = false;
                var v = GetValueInLogScale(((firstGreen + y) / 2) - xaxis, firstYScale, lastYScale);
                if (v > maxGreen) maxGreen = v;
                if (v < minGreen) minGreen = v;
                
                *(ptr + (((firstGreen + y) / 2) * stride) + x) = (uint)0xff8a00ff;
            }
            
            //B
            if ((pixel & 0x000000FF) != 0 && (pixel & 0xFF000000) != 0)
            {
                if (!inBlue) firstBlue = y;
                inBlue = true;
            } 
            else if (inBlue)
            {
                inBlue = false;
                var v = GetValueInLogScale(((firstBlue + y) / 2) - xaxis, firstYScale, lastYScale);
                if (v > maxBlue) maxBlue = v;
                if (v < minBlue) minBlue = v;
                
                *(ptr + (((firstBlue + y) / 2) * stride) + x) = (uint)0xffffde00;
            }
        }
        
        points[dx] = (xValue, 100*xValue/100, minRed, minGreen, minBlue);
        if (100*xValue < 1 || 100*xValue > 10)
            points[width-yaxis+dx] = (100*xValue, 100*xValue, maxRed, maxGreen, maxBlue);
        else
            points[width-yaxis+dx] = (100*xValue, 100*xValue, -1, -1, -1);
        
        //Console.WriteLine($"Pixel: {x}, Value: {xValue}, {minRed} {maxRed}  {minGreen} {maxGreen}  {minBlue} {maxBlue}");
    }

    var treatedPoints = (
        from p in points 
        where !(p.r < 0) && !(p.b < 0) && !(p.g < 0) && !(p.r > 10) && !(p.g > 10) && !(p.b > 10)
        orderby p.x
            select (p.x, r: Math.Clamp(p.r, 0, 1), g: Math.Clamp(p.g, 0, 1), b: Math.Clamp(p.b, 0, 1)))
        .ToList();

    Console.WriteLine("X. two. one. half");
    for (var index = 0; index < treatedPoints.Count - 1; index++)
    {
        var p = treatedPoints[index];

        //if (p.r - treatedPoints[index + 1].r > 0.08)
        //    p.r = (p.r + treatedPoints[index + 1].r + treatedPoints[index - 1].r) / 3;  
        
        Console.WriteLine($"{p.x}. {p.r}. {p.g}. {p.b}", CultureInfo.InvariantCulture);
    }

    double GetValueInLogScale(int pixelDelta, (int pixel, double value) first, (int pixel, double value) last)
    {
        var spanPixel = last.pixel - first.pixel;
        var spanLog = Math.Log10(last.value / first.value);

        return Math.Pow(10, spanLog * pixelDelta / spanPixel) * first.value;
    }

    bitmap.UnlockBits(imageData);
    bitmap.Save("D:\\Coding\\Jupiter\\FeijaoRip\\FeijaoAlphaCulled.png");
}
