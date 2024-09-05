
using System.Collections.Generic;

namespace Utils
{
    public class MathfExtension
    {
        public static int GetRandomIntValue(int min, int max, int except)
        {
            int v;
            do
            {
                v = UnityEngine.Random.Range(min, max);
            } while (v == except);

            return v;
        }

        public static int GetRandom(int max)
        {
            return UnityEngine.Random.Range(0, max);
        }

        public static T GetRandomItemInList<T>(List<T> list)
        {
            var index = GetRandom(list.Count);
            return list[index];
        }
    }
}