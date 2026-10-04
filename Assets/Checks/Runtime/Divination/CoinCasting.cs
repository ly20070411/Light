using System;

namespace Emerge.Checks.Divination
{
    [Serializable]
    public sealed class CoinCastResult
    {
        public int seed;
        public int[] coinFaces;
        public int[] yaoValues;
    }

    public static class CoinCasting
    {
        // Faces are frozen before presentation: 0 = back (2), 1 = front (3).
        public static CoinCastResult Cast(int seed)
        {
            var random = new Random(seed);
            var faces = new int[18];
            for (int i = 0; i < faces.Length; i++) faces[i] = random.Next(0, 2);
            return FromFaces(seed, faces);
        }

        public static CoinCastResult FromFaces(int seed, int[] faces)
        {
            if (faces == null || faces.Length != 18)
                throw new ArgumentException("起卦必须包含六次投掷、每次三枚铜钱，共十八个结果。", nameof(faces));
            var values = new int[6];
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] != 0 && faces[i] != 1)
                    throw new ArgumentException("铜钱结果只能为 0（背面）或 1（正面）。", nameof(faces));
                values[i / 3] += 2 + faces[i];
            }
            return new CoinCastResult { seed = seed, coinFaces = (int[])faces.Clone(), yaoValues = values };
        }

        public static bool IsValid(CoinCastResult result)
        {
            if (result == null || result.coinFaces == null || result.coinFaces.Length != 18 ||
                result.yaoValues == null || result.yaoValues.Length != 6) return false;
            for (int line = 0; line < 6; line++)
            {
                int expected = 0;
                for (int coin = 0; coin < 3; coin++)
                {
                    int face = result.coinFaces[line * 3 + coin];
                    if (face != 0 && face != 1) return false;
                    expected += 2 + face;
                }
                if (result.yaoValues[line] != expected) return false;
            }
            return true;
        }
    }
}
