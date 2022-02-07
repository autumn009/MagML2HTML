using System;

namespace SimpleCharTestLib
{
    public class SimpleCharTest
    {
        public static bool IsAlpha(char ch)
        {
            return (ch >= 'A' && ch <= 'Z')
                || (ch >= 'a' && ch <= 'z');
        }
        public static bool IsDigit(char ch)
        {
            return ch >= '0' && ch <= '9';
        }
        public static bool IsAlphaOrDigit(char ch)
        {
            return IsAlpha(ch) || IsDigit(ch);
        }
    }
}
