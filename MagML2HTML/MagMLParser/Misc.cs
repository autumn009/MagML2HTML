using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagMLParser
{
    public struct ItemID : System.IComparable
    {
        public const string ItemIDDateTimeFormat = "yyyyMMddHHmmss";
        public readonly string ID;
        public override string ToString()
        {
            return ID;
        }
        public ItemID(string id)
        {
            ID = id;
        }
        public ItemID(DateTime idDateTime)
        {
            ID = idDateTime.ToString(ItemIDDateTimeFormat);
        }
        public override bool Equals(object obj)
        {
            return ID == ((ItemID)obj).ID;
        }
        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
        public int CompareTo(object obj)
        {
            return ID.CompareTo(((ItemID)obj).ID);
        }
        public static bool operator ==(ItemID x, ItemID y)
        {
            if ((object)x == null && (object)y == null) return true;
            if ((object)x == null || (object)y == null) return false;
            return x.ID == y.ID;
        }
        public static bool operator !=(ItemID x, ItemID y)
        {
            if ((object)x == null && (object)y == null) return false;
            if ((object)x == null || (object)y == null) return true;
            return x.ID != y.ID;
        }
    }
    public class Size
    {
        public Size(int pixelWidth, int pixelHeight)
        {
            Width = pixelWidth;
            Height = pixelHeight;
        }

        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsEmpty { get; internal set; }
    }
}
