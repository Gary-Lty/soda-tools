using Newtonsoft.Json;
using UnityEngine;

namespace Utils
{
    public static class TextExtension
    {
        /// <summary>
        /// 使用个，万 ，亿的单位来显示缩写
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string ToAbbrText(this long num)
        {
            return TextUtil.GetAbbrText(num);
        }
        
        /// <summary>
        /// 使用个，万 ，亿的单位来显示缩写
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string ToAbbrText(this int num)
        {
            return TextUtil.GetAbbrText(num);
        }

        /// <summary>
        /// 获取省略的名字,最多显示10个字节
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string ToAbbrText(this string text)
        {
            return TextUtil.GetAbbrText(text);
        }

        /// <summary>
        /// 转为时间格式的string
        /// --:--.--
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static string ToTimeFormat(this float time)
        {
            return TextUtil.GetTime(time);
        }

        public static string ToTimeMinSec(this float time)
        {
            if (time == 0)
            {
                return "--:--.--";
            }
            var min = Mathf.FloorToInt(time / 60);
            var sec = time - min * 60;
            var secStr = sec < 10 ? "0"+sec.ToString("F0") : sec.ToString("F0");
            if (min < 10)
            {
                return "0" + min + ":" + secStr;
            }
            else
            {
                return min + ":" + secStr;
            }
        }
    }
    public class TextUtil
    {
        /// <summary>
        /// 获取省略的名字
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetAbbrText(string name)
        {
            if (name.Length < 8)
            {
                return name;
            }
            else
            {
                return name.Substring(0, 6) + "...";
            }
        }

        /// <summary>
        /// 积分使用个，万 ，亿的单位来显示分数
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public static string GetAbbrText(long count)
        {
            if (count < 9999)
            {
                return count.ToString();
            }
            else if (count < 99999999)
            {
                return (count / 10000f).ToString("F2") + "万";
            }
            else
            {
                return (count / 100000000).ToString("F2") + "亿";
            }
        }
        
        /// <summary>
        /// float 类型转为时间格式
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static string GetTime(float time)
        {
            if (time == 0)
            {
                return "--:--.--";
            }
            var min = Mathf.FloorToInt(time / 60);
            var sec = time - min * 60;
            var secStr = sec < 10 ? "0"+sec.ToString("F2") : sec.ToString("F2");
            if (min < 10)
            {
                return "0" + min + ":" + secStr;
            }
            else
            {
                return min + ":" + secStr;
            }
        }

        public static void LogJson(object target)
        {
            var json = JsonConvert.SerializeObject(target);
            Debug.LogWarning(json);
        }
    }
}