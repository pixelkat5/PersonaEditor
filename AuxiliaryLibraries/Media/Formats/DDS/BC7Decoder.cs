using System;

namespace AuxiliaryLibraries.Media.Formats.DDS
{
    public static class BC7Decoder
    {
        private static readonly int[] Weights2 = { 0, 21, 43, 64 };
        private static readonly int[] Weights3 = { 0, 9, 18, 27, 37, 46, 55, 64 };
        private static readonly int[] Weights4 = { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

        private static readonly int[][] Partition2 =
        {
            new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 },
            new int[] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 },
            new int[] { 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1 },
            new int[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1 },
            new int[] { 0, 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1 },
            new int[] { 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1 },
            new int[] { 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1 },
            new int[] { 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0 },
            new int[] { 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0 },
            new int[] { 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0 },
            new int[] { 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 1 },
            new int[] { 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0 },
            new int[] { 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0 },
            new int[] { 0, 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1, 1, 0, 0 },
            new int[] { 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 },
            new int[] { 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0 },
            new int[] { 0, 0, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0 },
            new int[] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 },
            new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1 },
            new int[] { 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0 },
            new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0 },
            new int[] { 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0 },
            new int[] { 0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0 },
            new int[] { 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0, 1 },
            new int[] { 0, 1, 0, 1, 1, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 1 },
            new int[] { 0, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 1, 0 },
            new int[] { 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0 },
            new int[] { 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 0 },
            new int[] { 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0, 0 },
            new int[] { 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0 },
            new int[] { 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1 },
            new int[] { 0, 1, 1, 0, 0, 1, 1, 0, 1, 0, 0, 1, 1, 0, 0, 1 },
            new int[] { 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0 },
            new int[] { 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 },
            new int[] { 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0 },
            new int[] { 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0 },
            new int[] { 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0 },
            new int[] { 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1 },
            new int[] { 0, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 0, 1 },
            new int[] { 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0 },
            new int[] { 0, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0 },
            new int[] { 0, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1 },
            new int[] { 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 1 },
            new int[] { 0, 1, 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1 },
            new int[] { 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 1, 0, 0, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 },
            new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 },
            new int[] { 0, 0, 1, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0 },
            new int[] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 1, 1, 1 },
        };

        private static readonly int[][] Partition3 =
        {
            new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 1, 2, 2, 2, 2 },
            new int[] { 0, 0, 0, 1, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 2, 1 },
            new int[] { 0, 0, 0, 0, 2, 0, 0, 1, 2, 2, 1, 1, 2, 2, 1, 1 },
            new int[] { 0, 2, 2, 2, 0, 0, 2, 2, 0, 0, 1, 1, 0, 1, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2 },
            new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 2, 2 },
            new int[] { 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1 },
            new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2 },
            new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2 },
            new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2 },
            new int[] { 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2 },
            new int[] { 0, 1, 1, 2, 0, 1, 1, 2, 0, 1, 1, 2, 0, 1, 1, 2 },
            new int[] { 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 2, 0, 1, 2, 2 },
            new int[] { 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 2, 1, 2, 2, 2 },
            new int[] { 0, 0, 1, 1, 2, 0, 0, 1, 2, 2, 0, 0, 2, 2, 2, 0 },
            new int[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 1, 2, 1, 1, 2, 2 },
            new int[] { 0, 1, 1, 1, 0, 0, 1, 1, 2, 0, 0, 1, 2, 2, 0, 0 },
            new int[] { 0, 0, 0, 0, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1, 2, 2 },
            new int[] { 0, 0, 2, 2, 0, 0, 2, 2, 0, 0, 2, 2, 1, 1, 1, 1 },
            new int[] { 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2, 0, 2, 2, 2 },
            new int[] { 0, 0, 0, 1, 0, 0, 0, 1, 2, 2, 2, 1, 2, 2, 2, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 2, 2, 0, 1, 2, 2 },
            new int[] { 0, 0, 0, 0, 1, 1, 0, 0, 2, 2, 1, 0, 2, 2, 1, 0 },
            new int[] { 0, 1, 2, 2, 0, 1, 2, 2, 0, 0, 1, 1, 0, 0, 0, 0 },
            new int[] { 0, 0, 1, 2, 0, 0, 1, 2, 1, 1, 2, 2, 2, 2, 2, 2 },
            new int[] { 0, 1, 1, 0, 1, 2, 2, 1, 1, 2, 2, 1, 0, 1, 1, 0 },
            new int[] { 0, 0, 0, 0, 0, 1, 1, 0, 1, 2, 2, 1, 1, 2, 2, 1 },
            new int[] { 0, 0, 2, 2, 1, 1, 0, 2, 1, 1, 0, 2, 0, 0, 2, 2 },
            new int[] { 0, 1, 1, 0, 0, 1, 1, 0, 2, 0, 0, 2, 2, 2, 2, 2 },
            new int[] { 0, 0, 1, 1, 0, 1, 2, 2, 0, 1, 2, 2, 0, 0, 1, 1 },
            new int[] { 0, 0, 0, 0, 2, 0, 0, 0, 2, 2, 1, 1, 2, 2, 2, 1 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 2, 2, 2 },
            new int[] { 0, 2, 2, 2, 0, 0, 2, 2, 0, 0, 1, 2, 0, 0, 1, 1 },
            new int[] { 0, 0, 1, 1, 0, 0, 1, 2, 0, 0, 2, 2, 0, 2, 2, 2 },
            new int[] { 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0, 0, 1, 2, 0 },
            new int[] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 0 },
            new int[] { 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0 },
            new int[] { 0, 1, 2, 0, 2, 0, 1, 2, 1, 2, 0, 1, 0, 1, 2, 0 },
            new int[] { 0, 0, 1, 1, 2, 2, 0, 0, 1, 1, 2, 2, 0, 0, 1, 1 },
            new int[] { 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 0, 0, 0, 0, 1, 1 },
            new int[] { 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 2, 1, 2, 1, 2, 1 },
            new int[] { 0, 0, 2, 2, 1, 1, 2, 2, 0, 0, 2, 2, 1, 1, 2, 2 },
            new int[] { 0, 0, 2, 2, 0, 0, 1, 1, 0, 0, 2, 2, 0, 0, 1, 1 },
            new int[] { 0, 2, 2, 0, 1, 2, 2, 1, 0, 2, 2, 0, 1, 2, 2, 1 },
            new int[] { 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 0, 1, 0, 1 },
            new int[] { 0, 0, 0, 0, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1 },
            new int[] { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 2, 2, 2, 2 },
            new int[] { 0, 2, 2, 2, 0, 1, 1, 1, 0, 2, 2, 2, 0, 1, 1, 1 },
            new int[] { 0, 0, 0, 2, 1, 1, 1, 2, 0, 0, 0, 2, 1, 1, 1, 2 },
            new int[] { 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 1, 2, 2, 1, 1, 2 },
            new int[] { 0, 2, 2, 2, 0, 1, 1, 1, 0, 1, 1, 1, 0, 2, 2, 2 },
            new int[] { 0, 0, 0, 2, 1, 1, 1, 2, 1, 1, 1, 2, 0, 0, 0, 2 },
            new int[] { 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 2, 2 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2, 2, 1, 1, 2 },
            new int[] { 0, 1, 1, 0, 0, 1, 1, 0, 2, 2, 2, 2, 2, 2, 2, 2 },
            new int[] { 0, 0, 2, 2, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 2, 2 },
            new int[] { 0, 0, 2, 2, 1, 1, 2, 2, 1, 1, 2, 2, 0, 0, 2, 2 },
            new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 2 },
            new int[] { 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 1 },
            new int[] { 0, 2, 2, 2, 1, 2, 2, 2, 0, 2, 2, 2, 1, 2, 2, 2 },
            new int[] { 0, 1, 0, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 },
            new int[] { 0, 1, 1, 1, 2, 0, 1, 1, 2, 2, 0, 1, 2, 2, 2, 0 },
        };

        private static readonly int[] Anchor2 = { 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 2, 8, 2, 2, 8, 8, 15, 2, 8, 2, 2, 8, 8, 2, 2, 15, 15, 6, 8, 2, 8, 15, 15, 2, 8, 2, 2, 2, 15, 15, 6, 6, 2, 6, 8, 15, 15, 2, 2, 15, 15, 15, 15, 15, 2, 2, 15 };
        private static readonly int[] Anchor3First = { 3, 3, 15, 15, 8, 3, 15, 15, 8, 8, 6, 6, 6, 5, 3, 3, 3, 3, 8, 15, 3, 3, 6, 10, 5, 8, 8, 6, 8, 5, 15, 15, 8, 15, 3, 5, 6, 10, 8, 15, 15, 3, 15, 5, 15, 15, 15, 15, 3, 15, 5, 5, 5, 8, 5, 10, 5, 10, 8, 13, 15, 12, 3, 3 };
        private static readonly int[] Anchor3Second = { 15, 8, 8, 3, 15, 15, 3, 8, 15, 15, 15, 15, 15, 15, 15, 8, 15, 8, 15, 3, 15, 8, 15, 8, 3, 15, 6, 10, 15, 15, 10, 8, 15, 3, 15, 10, 10, 8, 9, 10, 6, 15, 8, 15, 3, 6, 6, 8, 15, 3, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 3, 15, 15, 8 };

        private class ModeInfo
        {
            public int Subsets;
            public int PartitionBits;
            public int RotationBits;
            public int IndexSelectionBits;
            public int ColorBits;
            public int AlphaBits;
            public int EndpointPBits;
            public int IndexBits;
            public int SecondaryIndexBits;

            public ModeInfo(int subsets, int partitionBits, int rotationBits, int indexSelectionBits, int colorBits, int alphaBits, int endpointPBits, int indexBits, int secondaryIndexBits)
            {
                Subsets = subsets;
                PartitionBits = partitionBits;
                RotationBits = rotationBits;
                IndexSelectionBits = indexSelectionBits;
                ColorBits = colorBits;
                AlphaBits = alphaBits;
                EndpointPBits = endpointPBits;
                IndexBits = indexBits;
                SecondaryIndexBits = secondaryIndexBits;
            }
        }

        private static readonly ModeInfo[] Modes =
        {
            new ModeInfo(subsets: 3, partitionBits: 4, rotationBits: 0, indexSelectionBits: 0, colorBits: 4, alphaBits: 0, endpointPBits: 1, indexBits: 3, secondaryIndexBits: 0),
            new ModeInfo(subsets: 2, partitionBits: 6, rotationBits: 0, indexSelectionBits: 0, colorBits: 6, alphaBits: 0, endpointPBits: 2, indexBits: 3, secondaryIndexBits: 0),
            new ModeInfo(subsets: 3, partitionBits: 6, rotationBits: 0, indexSelectionBits: 0, colorBits: 5, alphaBits: 0, endpointPBits: 0, indexBits: 2, secondaryIndexBits: 0),
            new ModeInfo(subsets: 2, partitionBits: 6, rotationBits: 0, indexSelectionBits: 0, colorBits: 7, alphaBits: 0, endpointPBits: 1, indexBits: 2, secondaryIndexBits: 0),
            new ModeInfo(subsets: 1, partitionBits: 0, rotationBits: 2, indexSelectionBits: 1, colorBits: 5, alphaBits: 6, endpointPBits: 0, indexBits: 2, secondaryIndexBits: 3),
            new ModeInfo(subsets: 1, partitionBits: 0, rotationBits: 2, indexSelectionBits: 0, colorBits: 7, alphaBits: 8, endpointPBits: 0, indexBits: 2, secondaryIndexBits: 2),
            new ModeInfo(subsets: 1, partitionBits: 0, rotationBits: 0, indexSelectionBits: 0, colorBits: 7, alphaBits: 7, endpointPBits: 1, indexBits: 4, secondaryIndexBits: 0),
            new ModeInfo(subsets: 2, partitionBits: 6, rotationBits: 0, indexSelectionBits: 0, colorBits: 5, alphaBits: 5, endpointPBits: 1, indexBits: 2, secondaryIndexBits: 0),
        };

        private class BitReader
        {
            private ulong _low;
            private ulong _high;

            public BitReader(byte[] block, int offset)
            {
                _low = BitConverter.ToUInt64(block, offset);
                _high = BitConverter.ToUInt64(block, offset + 8);
            }

            public int Read(int count)
            {
                if (count == 0)
                {
                    return 0;
                }

                int value = (int)(_low & ((1UL << count) - 1));
                _low >>= count;
                _low |= (_high & ((1UL << count) - 1)) << (64 - count);
                _high >>= count;
                return value;
            }
        }

        private static int ExpandBits(int value, int sourceBits, bool hasPBit, int pBit)
        {
            if (hasPBit)
            {
                value = (value << 1) | pBit;
                sourceBits += 1;
            }

            if (sourceBits >= 8)
            {
                return value & 0xFF;
            }

            value <<= 8 - sourceBits;
            return value | (value >> sourceBits);
        }

        private static int Interpolate(int e0, int e1, int index, int[] weights)
        {
            int w = weights[index];
            return (e0 * (64 - w) + e1 * w + 32) >> 6;
        }

        private static int[] WeightsForBits(int bits)
        {
            if (bits == 2)
            {
                return Weights2;
            }

            if (bits == 3)
            {
                return Weights3;
            }

            return Weights4;
        }

        private static void DecodeBlock(byte[] src, int srcOffset, byte[] dst, int dstOffset, int dstStride, int blockWidth, int blockHeight)
        {
            var br = new BitReader(src, srcOffset);

            int mode = 8;
            for (int m = 0; m < 8; m++)
            {
                if (br.Read(1) == 1)
                {
                    mode = m;
                    break;
                }
            }

            if (mode == 8)
            {
                for (int y = 0; y < blockHeight; y++)
                {
                    for (int x = 0; x < blockWidth; x++)
                    {
                        int o = dstOffset + y * dstStride + x * 4;
                        dst[o] = 0;
                        dst[o + 1] = 0;
                        dst[o + 2] = 0;
                        dst[o + 3] = 0;
                    }
                }
                return;
            }

            ModeInfo info = Modes[mode];
            int ns = info.Subsets;

            int partition = info.PartitionBits > 0 ? br.Read(info.PartitionBits) : 0;
            int rotation = info.RotationBits > 0 ? br.Read(info.RotationBits) : 0;
            int indexSelection = info.IndexSelectionBits > 0 ? br.Read(info.IndexSelectionBits) : 0;

            int[] prow;
            if (ns == 1)
            {
                prow = null;
            }
            else if (ns == 2)
            {
                prow = Partition2[partition];
            }
            else
            {
                prow = Partition3[partition];
            }

            int numEndpoints = ns * 2;
            int[,] colors = new int[numEndpoints, 3];
            int[] alphas = new int[numEndpoints];

            for (int c = 0; c < 3; c++)
            {
                for (int e = 0; e < numEndpoints; e++)
                {
                    colors[e, c] = br.Read(info.ColorBits);
                }
            }

            if (info.AlphaBits > 0)
            {
                for (int e = 0; e < numEndpoints; e++)
                {
                    alphas[e] = br.Read(info.AlphaBits);
                }
            }

            int[] pbits = new int[numEndpoints];
            for (int e = 0; e < numEndpoints; e++)
            {
                pbits[e] = 1;
            }

            bool hasPBit = info.EndpointPBits != 0;
            if (info.EndpointPBits == 1)
            {
                for (int e = 0; e < numEndpoints; e++)
                {
                    pbits[e] = br.Read(1);
                }
            }
            else if (info.EndpointPBits == 2)
            {
                int[] subsetPBits = new int[ns];
                for (int s = 0; s < ns; s++)
                {
                    subsetPBits[s] = br.Read(1);
                }

                for (int e = 0; e < numEndpoints; e++)
                {
                    pbits[e] = subsetPBits[e / 2];
                }
            }

            for (int e = 0; e < numEndpoints; e++)
            {
                for (int c = 0; c < 3; c++)
                {
                    colors[e, c] = ExpandBits(colors[e, c], info.ColorBits, hasPBit, pbits[e]);
                }

                if (info.AlphaBits > 0)
                {
                    alphas[e] = ExpandBits(alphas[e], info.AlphaBits, hasPBit, pbits[e]);
                }
                else
                {
                    alphas[e] = 255;
                }
            }

            int[] anchors = new int[ns];
            anchors[0] = 0;
            if (ns == 2)
            {
                anchors[1] = Anchor2[partition];
            }
            else if (ns == 3)
            {
                anchors[1] = Anchor3First[partition];
                anchors[2] = Anchor3Second[partition];
            }

            int ib = info.IndexBits;
            int ib2 = info.SecondaryIndexBits;

            int[] primaryIdx = new int[16];
            for (int i = 0; i < 16; i++)
            {
                int subset = ns == 1 ? 0 : prow[i];
                int nb = i == anchors[subset] ? ib - 1 : ib;
                primaryIdx[i] = br.Read(nb);
            }

            int[] secondaryIdx = null;
            if (ib2 > 0)
            {
                secondaryIdx = new int[16];
                for (int i = 0; i < 16; i++)
                {
                    int subset = ns == 1 ? 0 : prow[i];
                    int nb = i == anchors[subset] ? ib2 - 1 : ib2;
                    secondaryIdx[i] = br.Read(nb);
                }
            }

            for (int i = 0; i < 16; i++)
            {
                int px = i % 4;
                int py = i / 4;
                if (px >= blockWidth || py >= blockHeight)
                {
                    continue;
                }

                int subset = ns == 1 ? 0 : prow[i];
                int e0 = subset * 2;
                int e1 = subset * 2 + 1;

                int colorIdx = primaryIdx[i];
                int[] colorWeights = WeightsForBits(ib);
                int alphaIdx = primaryIdx[i];
                int[] alphaWeights = colorWeights;

                if (secondaryIdx != null)
                {
                    if (indexSelection == 0)
                    {
                        alphaIdx = secondaryIdx[i];
                        alphaWeights = WeightsForBits(ib2);
                    }
                    else
                    {
                        colorIdx = secondaryIdx[i];
                        colorWeights = WeightsForBits(ib2);
                        alphaIdx = primaryIdx[i];
                        alphaWeights = WeightsForBits(ib);
                    }
                }

                int r = Interpolate(colors[e0, 0], colors[e1, 0], colorIdx, colorWeights);
                int g = Interpolate(colors[e0, 1], colors[e1, 1], colorIdx, colorWeights);
                int b = Interpolate(colors[e0, 2], colors[e1, 2], colorIdx, colorWeights);
                int a = info.AlphaBits > 0 ? Interpolate(alphas[e0], alphas[e1], alphaIdx, alphaWeights) : 255;

                if (rotation == 1)
                {
                    int t = r;
                    r = a;
                    a = t;
                }
                else if (rotation == 2)
                {
                    int t = g;
                    g = a;
                    a = t;
                }
                else if (rotation == 3)
                {
                    int t = b;
                    b = a;
                    a = t;
                }

                int o = dstOffset + py * dstStride + px * 4;
                dst[o] = (byte)b;
                dst[o + 1] = (byte)g;
                dst[o + 2] = (byte)r;
                dst[o + 3] = (byte)a;
            }
        }

        public static byte[] Decode(byte[] data, int width, int height)
        {
            byte[] output = new byte[width * height * 4];
            int stride = width * 4;
            int blocksWide = (width + 3) / 4;
            int blocksHigh = (height + 3) / 4;

            for (int by = 0; by < blocksHigh; by++)
            {
                for (int bx = 0; bx < blocksWide; bx++)
                {
                    int srcOffset = (by * blocksWide + bx) * 16;
                    if (srcOffset + 16 > data.Length)
                    {
                        continue;
                    }

                    int dstOffset = (by * 4) * stride + (bx * 4) * 4;
                    int blockWidth = Math.Min(4, width - bx * 4);
                    int blockHeight = Math.Min(4, height - by * 4);
                    DecodeBlock(data, srcOffset, output, dstOffset, stride, blockWidth, blockHeight);
                }
            }

            return output;
        }
    }
}
