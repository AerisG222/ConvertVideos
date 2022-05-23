using System;

namespace ConvertVideos;

public class ScaledDimensions
{
    public int ScaledHeight { get; private set; }
    public int ScaledWidth { get; private set; }


    public ScaledDimensions(int minDimension, float srcHeight, float srcWidth)
    {
        if (srcHeight <= srcWidth)
        {
            ScaledHeight = minDimension;
            ScaledWidth = CalculateScaledDimension(minDimension, srcHeight, srcWidth);
        }
        else
        {
            ScaledWidth = minDimension;
            ScaledHeight = CalculateScaledDimension(minDimension, srcWidth, srcHeight);
        }
    }


    int CalculateScaledDimension(int minDimension, float actualMinDimension, float actualMaxDimension)
    {
        float maxDimension = (float)minDimension * (actualMaxDimension / actualMinDimension);

        int value = (int)Math.Truncate(maxDimension);

        // dimensions must be a multiple of 2 for h264
        if (value % 2 != 0)
        {
            value++;
        }

        return value;
    }
}
