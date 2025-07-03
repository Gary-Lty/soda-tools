using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


    public class FpsCounter : MonoBehaviour
    {
        float sum;
        List<float> frameTimes = new();
        public int updateFrames = 60;
        private int i;
        public TextMeshPro content;

        void Update()
        {
            float t = Time.deltaTime;
            sum += t;
            frameTimes.Add(t);

            int l = frameTimes.Count;
            if (l == updateFrames)
            {
                frameTimes.Sort();

                int n = updateFrames / 2;

                float medianDeltaTime;
                // even
                if (updateFrames - n * 2 == 0)
                {
                    medianDeltaTime = (frameTimes[n - 1] + frameTimes[n]) * 0.5f;
                }
                // odd
                else
                {
                    medianDeltaTime = frameTimes[n];
                }

                float avg = ((float)l / sum); // average fps value
                float med = 1f / medianDeltaTime; // half of the frames were above this fps value

                i++;

                content.text = string.Format("FPS:\n{0:f2} (average)\n{1:f2} (median)", avg, med);

                frameTimes.Clear();
                sum = 0f;
            }
        }
    }
