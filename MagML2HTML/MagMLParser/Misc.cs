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
        public DateTime GetDateOnly()
        {
            return DateTime.ParseExact(ID.Substring(0, 8), "yyyyMMdd", null);
        }
        public DateTime GetDateTime()
        {
            if (ID.Length < 14) return DateTime.MinValue;
            DateTime result = DateTime.MinValue;
            DateTime.TryParseExact(ID.Substring(0, 14), ItemIDDateTimeFormat, null, System.Globalization.DateTimeStyles.None, out result);
            return result;
        }
        public bool IsNoItem()
        {
            return ID == "00000000000000";
        }
        public static ItemID NoItem = new ItemID("00000000000000");
        public static ItemID CreateDraftItemID(int n)
        {
            DateTime d = DateTime.MinValue.AddSeconds(n);
            if (d.Year != 1)
            {
                throw new ApplicationException(n.ToString() + "はTemporaryItemIDとして使用できない大きすぎる値です。");
            }
            return new ItemID(d);
        }
        public bool IsUnderConstructionDraftItemID()
        {
            return ID.StartsWith("0001");
        }
        public bool IsDuplicatedDraftItemID()
        {
            return int.Parse(ID.Substring(0, 4)) >= 5000;
        }
        public bool IsDraftItemID()
        {
            return IsUnderConstructionDraftItemID() || IsDuplicatedDraftItemID();
        }
        public bool IsNormalItemID()
        {
            int year = int.Parse(ID.Substring(0, 4));
            return year > 3 && year < 5000;
        }
        public bool IsAboutKeywordItemID()
        {
            return ID.StartsWith("0002");
        }
        public bool IsCommonNoticeItemID()
        {
            return ID.StartsWith("0003");
        }
        public ItemID ToNormalItemID()
        {
            if (!IsDuplicatedDraftItemID())
            {
                throw new ApplicationException(this.ID + "は既に通常のコンテンツIDです。通常のコンテンツIDに変換することはできません。");
            }
            return new ItemID(GetDateTime().AddYears(-5000));
        }
        public ItemID ToDuplicatedDraftItemID()
        {
            if (IsDraftItemID())
            {
                throw new ApplicationException(this.ID + "は既にDraftコンテンツIDです。DuplicatedDraftコンテンツIDに変換することはできません。");
            }
            return new ItemID(GetDateTime().AddYears(5000));
        }
        public static bool IsValidIDFormat(string idString)
        {
            DateTime result = DateTime.MinValue;
            return DateTime.TryParseExact(idString, ItemIDDateTimeFormat, null, System.Globalization.DateTimeStyles.None, out result);
        }
    }
    public class FullKeyword : System.IComparable
    {
        private const string rootKeywordString = "▲";
        public static readonly FullKeyword NoKeyword = new FullKeyword();
        public static readonly FullKeyword RootKeyword = new FullKeyword(rootKeywordString);
        public bool IsNoKeyword()
        {
            return Keywords.Length == 0;
        }
        public bool IsRootKeyword()
        {
            return Keywords.Length == 1 && Keywords[0] == rootKeywordString;
        }
        public readonly string[] Keywords;
        public readonly string EncodedKeyword;
        public override string ToString()
        {
            return EncodedKeyword;
        }
        public bool IsSubKeyword(FullKeyword target)
        {
            if (Keywords.Length >= target.Keywords.Length) return false;
            for (int i = 0; i < Keywords.Length; i++)
            {
                if (Keywords[i] != target.Keywords[i]) return false;
            }
            return true;
        }
        public FullKeyword GetParentKeyword()
        {
            if (Keywords.Length == 0)
            {
                throw new ApplicationException("NoKeywordの親キーワードはありません。");
            }
            string[] newkw = new string[Keywords.Length - 1];
            for (int i = 0; i < newkw.Length; i++)
            {
                newkw[i] = Keywords[i];
            }
            return new FullKeyword(newkw);
        }
        public FullKeyword(string encodedKeyword)
        {
            if (encodedKeyword.Length == 0)
            {
                Keywords = new string[0];
                EncodedKeyword = "";
                return;
            }
            EncodedKeyword = encodedKeyword;
            ArrayList keywords = new ArrayList();
            string[] tokens = EncodedKeyword.Split('_');
            foreach (string token in tokens)
            {
                if (token == "_") continue;
                keywords.Add(KeywordFileNameEncoder.Decode(token));
            }
            Keywords = (string[])keywords.ToArray(typeof(string));
        }
        private FullKeyword()
        {
            Keywords = new string[0];
            EncodedKeyword = "";
        }
        public FullKeyword(string[] keywords)
        {
            Keywords = keywords;
            if (keywords.Length == 0)
            {
                EncodedKeyword = "";
                return;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(KeywordFileNameEncoder.Encode(keywords[0]));
            for (int i = 1; i < keywords.Length; i++)
            {
                sb.Append('_');
                sb.Append(KeywordFileNameEncoder.Encode(keywords[i]));
            }
            EncodedKeyword = sb.ToString();
        }
        public override bool Equals(object? obj)
        {
            FullKeyword f = ((FullKeyword?)obj) ?? FullKeyword.NoKeyword;
            return EncodedKeyword == f.EncodedKeyword;
        }
        public override int GetHashCode()
        {
            return EncodedKeyword.GetHashCode();
        }
        public int CompareTo(object? obj)
        {
            FullKeyword f = ((FullKeyword?)obj) ?? FullKeyword.NoKeyword;
            return EncodedKeyword.CompareTo(f.EncodedKeyword);
        }
        public static bool operator ==(FullKeyword? x, FullKeyword? y)
        {
            if ((object?)x == null && (object?)y == null) return true;
            if ((object?)x == null || (object?)y == null) return false;
            return x.EncodedKeyword == y.EncodedKeyword;
        }
        public static bool operator !=(FullKeyword? x, FullKeyword? y)
        {
            if ((object?)x == null && (object?)y == null) return false;
            if ((object?)x == null || (object?)y == null) return true;
            return x.EncodedKeyword != y.EncodedKeyword;
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
    public class ResizeManager
    {
        internal static int[] GetResizeWidths()
        {
            throw new NotImplementedException();
        }
    }
    public class ContentEntry
    {
        public readonly ItemID ID;
        public readonly FullKeyword Keyword;
    }
    public class ContentFileAccessLayer
    {
        internal static bool IsExist(ItemID itemID)
        {
            throw new NotImplementedException();
        }

        internal static Item GetItem(ItemID itemID)
        {
            throw new NotImplementedException();
        }

        internal static IEnumerable<ContentEntry> EnumetateItemIDsIncludingSubKeywords(FullKeyword fullKeyword)
        {
            throw new NotImplementedException();
        }

        internal static void ClearCache()
        {
            throw new NotImplementedException();
        }
    }
    public class XmlUtility
    {
        public static string EscapeStringByPredefinedEntities(string src)
        {
            return src.Replace("&", "&amp;").Replace("<", "&lt;");
        }
        public static string EscapeStringByPredefinedEntitiesIncludingQuote(string src)
        {
            return src.Replace("&", "&amp;").Replace("<", "&lt;").Replace("'", "&apos;").Replace("\"", "&quot;");
        }
    }
    // ML4から丸ごと持ってきたクラスだが、'_'を許さない文字に追加している点が異なるので、同一ではない
    public class KeywordFileNameEncoder
    {
        private const string reserved = "<>:\"/\\|?*%_";
        public static string Encode(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (reserved.IndexOf(c) >= 0)
                {
                    sb.Append('%');
                    sb.Append(string.Format("{0:X2}", (int)c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        public static string Decode(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int p = 0;
            while (true)
            {
                if (p >= s.Length) break;
                if (s[p] == '%')
                {
                    int n = int.Parse(s.Substring(p + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    sb.Append((char)n);
                    p += 3;
                }
                else
                {
                    sb.Append(s[p]);
                    p++;
                }
            }
            return sb.ToString();
        }
    }
    public class SectionDef
    {
        public string ShortKeyword => throw new NotImplementedException();
    }
    public class SectionDefManager
    {
        internal static SectionDef GetSectionDef(FullKeyword targetKeyword)
        {
            throw new NotImplementedException();
        }
    }
    public class ShortKeywordFinder
    {
        internal static bool IsShortKeyword(string shortName)
        {
            throw new NotImplementedException();
        }

        internal static FullKeyword GetFullKeyword(string shortName)
        {
            throw new NotImplementedException();
        }
    }
    public class SafeFileStream
    {
        public static FileStream CreateFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            //if( mode == FileMode.Open && !File.Exists(path) )
            //{
            //	throw new FileNotFoundException("ファイルが見つかりません。", path );
            //}
            int count = 0;
            while (true)
            {
                try
                {
                    FileStream stream = new FileStream(path, mode, access, share);
                    return stream;
                }
                catch (FileNotFoundException)
                {
                    throw;
                }
                catch (IOException)
                {
                    count++;
                    if (count > 10)
                    {
                        throw;
                    }
                }
                System.Threading.Thread.Sleep(100);
            }
        }
    }

    public class ContentAccessRanking
    {
        internal static object GetTotalAccess(ItemID id)
        {
            throw new NotImplementedException();
        }

        internal static object GetAccessRanking(FullKeyword keyword, DateTime today)
        {
            throw new NotImplementedException();
        }
    }


}
