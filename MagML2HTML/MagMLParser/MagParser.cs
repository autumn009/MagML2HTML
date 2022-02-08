	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Web;
	using System.IO;
	using System.Linq;
	using NestedHtmlWriter;
	using SimpleCharTestLib;

namespace MagMLParser
{
	public enum EMagTokenType
	{
		MarkupStart,
		MarkupEnd,
		Text,
		NoCookText,
		Newline,
		EOF,
		XhtmlFragment
	}

	public class MagToken
	{
		public readonly EMagTokenType Type;
		public readonly string Value;
		public MagToken(EMagTokenType type, string val)
		{
			Type = type;
			Value = val;
		}
		public MagToken(EMagTokenType type)
		{
			Type = type;
			Value = string.Empty;
		}
	}

	public class MagTokenizer
	{
		private const string dollerDoller = "$$";
		private string src;
		private int p = 0;
		public static bool IsSpaceCharacter(char ch)
		{
			return ch == ' ' || ch == '　' || ch == '\t';
		}
		private struct pair
		{
			public string Markup;
			public char Target;
			public pair(string markup, char target)
			{
				Markup = markup;
				Target = target;
			}
		}
		private pair[] specialCharcters =
			{
				new pair("$$$",'$'),
				new pair("$$z",'　'),
				new pair("$$h",'\u00a0'),
				new pair("$$y",'\u00a5')
			};
		private pair[] exceptCharcters =
			{
				new pair("$$,",','),
			};

		private char checkCharacter(string src, pair[] tableCharacters)
		{
			foreach (pair p in tableCharacters)
			{
				if (src.StartsWith(p.Markup))
				{
					return p.Target;
				}
			}
			return '\0';
		}

		public MagToken GetNextToken()
		{
			if (p >= src.Length)
			{
				return new MagToken(EMagTokenType.EOF);
			}
			MagToken result;
			string srcLeft = src.Substring(p);
			int len = srcLeft.Length;

			// この判定は本当は良くない。$$xhtmlが行頭から始まっていないケースでも受け付けてしまう
			if (len >= 8 && srcLeft.StartsWith("$$xhtml") && (srcLeft[7] == '\r' || srcLeft[7] == '\n'))
			{
				var content = new System.Text.StringBuilder();
				p += 8;
				if (len >= 9 && srcLeft[7] == '\r' && srcLeft[8] == '\n') p++;
				int lastPos = p;
				for (; ; )
				{
					if (p >= src.Length) break;
					var singleLine = new System.Text.StringBuilder();
					for (; ; )
					{
						lastPos = p;
						if (p >= src.Length) break;
						if (src.Length > p + 1 && src[p] == '\r' && src[p + 1] == '\n') { p += 2; break; }
						if (src[p] == '\r' || src[p] == '\n') { p++; break; }
						singleLine.Append(src[p++]);
					}
					if (singleLine.ToString() == "$$xhtml") break;
					content.AppendLine(singleLine.ToString());
				}
				// 最後の改行の手前まで戻す。改行トークンを生成させるため
				p = lastPos;
				return new MagToken(EMagTokenType.XhtmlFragment, content.ToString());
			}

			char specialCharacter = checkCharacter(srcLeft, specialCharcters);
			char exceptCharacter = checkCharacter(srcLeft, exceptCharcters);
			if (specialCharacter != '\0')
			{
				len = 3;
				result = new MagToken(EMagTokenType.Text, specialCharacter.ToString());
			}
			else if (exceptCharacter != '\0')
			{
				len = 3;
				result = new MagToken(EMagTokenType.NoCookText, exceptCharacter.ToString());
			}
			else if (srcLeft.StartsWith("$$!"))
			{
				len = 3;
				result = new MagToken(EMagTokenType.MarkupEnd);
			}
			else if (srcLeft.StartsWith(dollerDoller))
			{
				int p0 = 2;
				while (true)
				{
					if (p0 >= srcLeft.Length) break;
					if (srcLeft[p0] == '\r') break;
					if (srcLeft[p0] == '\n') break;
					if (srcLeft.Substring(p0).StartsWith(dollerDoller)) break;
					if (IsSpaceCharacter(srcLeft[p0])) break;
					p0++;
				}
				result = new MagToken(EMagTokenType.MarkupStart, srcLeft.Substring(2, p0 - 2));
				while (true)
				{
					if (p0 >= srcLeft.Length) break;
					if (!IsSpaceCharacter(srcLeft[p0])) break;
					p0++;
				}
				len = p0;
			}
			else if (srcLeft.StartsWith("\r") || srcLeft.StartsWith("\n"))
			{
				len = 1;
				result = new MagToken(EMagTokenType.Newline);
				if (srcLeft.Length >= 2 && srcLeft[0] == '\r' && srcLeft[1] == '\n')
				{
					len++;
				}
			}
			else
			{
				int p0 = 0;
				while (true)
				{
					if (p0 >= srcLeft.Length) break;
					if (srcLeft[p0] == '\r') break;
					if (srcLeft[p0] == '\n') break;
					if (srcLeft.Substring(p0).StartsWith(dollerDoller)) break;
					p0++;
				}

				len = p0;
				result = new MagToken(EMagTokenType.Text, srcLeft.Substring(0, len));
			}
			p += len;
			return result;
		}
		public MagTokenizer(string src)
		{
			this.src = src;
		}
	}

	public enum EMagNodeType
	{
		Root,
		LogicalLine,
		InlineMarkup,
		BlockMarkup,
		Text,
		NoCookText,
		ErrorText,
		XhtmlFragment,
	}

	public class MagNode
	{
		public readonly EMagNodeType Type;
		public readonly MagNode[] Children;
		public readonly string Value;
		public MagNode(EMagNodeType type, ArrayList children, string nodeValue)
		{
			Type = type;
			Children = (MagNode[])children.ToArray(typeof(MagNode));
			Value = nodeValue;
		}
		public MagNode(EMagNodeType type, ArrayList children)
			: this(type, children, string.Empty)
		{
		}
	}

	public class MagParser
	{
		public readonly MagNode MagTree;
		public static bool IsBlockMarkupName(string name)
		{
			string[] blockMarkupNames =
				{
					"1",
					"2",
					"3",
					"4",
					"5",
					"ul",
					"ol",
					">",
					"pre",
					"table",
					"pm",
					"fl",
					"cat",
					"xyt",
					"ctree",
					"bq",
					"gm",
					"gmp",
					"gml",
					"ltx",
					"fhide",
					"asin",
					"asinr",
					"isbn",
					"vc",
			};
			foreach (string blockMarkupName in blockMarkupNames)
			{
				if (name == blockMarkupName) return true;
			}
			return false;
		}
		public static bool IsInlineMarkupName(string name)
		{
			string[] inlineMarkupNames =
				{
					"_",
					"*",
					"/",
					"-",
					"a",
					"ae",
					"img",
					"wikipedia",
					"ref",
					"xhide",
					"q",
					"cl",
					"flash",
					"superq",
			};
			foreach (string inlineMarkupName in inlineMarkupNames)
			{
				if (name == inlineMarkupName) return true;
			}
			return false;
		}
		private MagToken parseLogicalBlock(MagTokenizer tokenizer, MagToken firstToken,
			ArrayList nodeList, bool topLevel)
		{
			MagToken token = firstToken;
			while (true)
			{
				if (token.Type == EMagTokenType.Newline || token.Type == EMagTokenType.EOF) break;
				if (!topLevel && token.Type == EMagTokenType.MarkupEnd)
				{
					return tokenizer.GetNextToken();
				}
				switch (token.Type)
				{
					case EMagTokenType.MarkupStart:
						ArrayList children = new ArrayList();
						string name = token.Value;
						token = parseLogicalBlock(tokenizer, tokenizer.GetNextToken(), children, false);
						if (IsBlockMarkupName(name))
						{
							nodeList.Add(new MagNode(EMagNodeType.BlockMarkup, children, name));
						}
						else if (IsInlineMarkupName(name))
						{
							nodeList.Add(new MagNode(EMagNodeType.InlineMarkup, children, name));
						}
						else
						{
							nodeList.Add(new MagNode(EMagNodeType.ErrorText, children, name + "はMagMLのマークアップの名前ではありません。"));
						}
						break;
					case EMagTokenType.MarkupEnd:
						nodeList.Add(new MagNode(EMagNodeType.Text, new ArrayList(), "$$!"));
						token = tokenizer.GetNextToken();
						break;
					case EMagTokenType.Text:
						nodeList.Add(new MagNode(EMagNodeType.Text, new ArrayList(), token.Value));
						token = tokenizer.GetNextToken();
						break;
					case EMagTokenType.NoCookText:
						nodeList.Add(new MagNode(EMagNodeType.NoCookText, new ArrayList(), token.Value));
						token = tokenizer.GetNextToken();
						break;
					case EMagTokenType.XhtmlFragment:
						nodeList.Add(new MagNode(EMagNodeType.XhtmlFragment, new ArrayList(), token.Value));
						return tokenizer.GetNextToken();
					default:
						System.Diagnostics.Trace.Fail("Internal Error: Unknown type of token:" + token.ToString());
						break;
				}
			}
			return token;
		}

		private MagNode normalizeText(MagNode node)
		{
			for (int i = 0; i < node.Children.Length; i++)
			{
				node.Children[i] = normalizeText(node.Children[i]);
			}

			ArrayList children = new ArrayList();
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (MagNode child in node.Children)
			{
				if (child.Type == EMagNodeType.Text)
				{
					sb.Append(child.Value);
				}
				else
				{
					if (sb.Length > 0)
					{
						children.Add(new MagNode(EMagNodeType.Text, new ArrayList(), sb.ToString()));
						sb = new System.Text.StringBuilder();
					}
					children.Add(child);
				}
			}
			if (sb.Length > 0)
			{
				children.Add(new MagNode(EMagNodeType.Text, new ArrayList(), sb.ToString()));
			}
			return new MagNode(node.Type, children, node.Value);
		}

		public MagParser(MagTokenizer tokenizer)
		{
			ArrayList rootNodeChildren = new ArrayList();
			while (true)
			{
				MagToken token = tokenizer.GetNextToken();
				if (token.Type == EMagTokenType.EOF) break;
				ArrayList children = new ArrayList();
				MagToken resultToken = parseLogicalBlock(tokenizer, token, children, true);
				System.Diagnostics.Debug.Assert(resultToken.Type == EMagTokenType.EOF
					|| resultToken.Type == EMagTokenType.Newline);
				rootNodeChildren.Add(new MagNode(EMagNodeType.LogicalLine, children));
			}
			MagTree = normalizeText(new MagNode(EMagNodeType.Root, rootNodeChildren));
		}
	}

	public class MagXHTMLGenerator
	{
		private const string xmlnsInterim = "http://www.piedey.co.jp/xmlns/modulaf/interim";

		public readonly string XhtmlFragmentString;
		public bool EncountedError = false;
		public readonly ArrayList Categories = new ArrayList();
		public readonly List<string> EmbeddedAsins = new List<string>();
		public readonly List<string> RepresentAsins = new List<string>();

		private static string getInnerText(MagNode node)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			if (node.Type == EMagNodeType.Text || node.Type == EMagNodeType.NoCookText || node.Type == EMagNodeType.ErrorText)
			{
				sb.Append(node.Value);
			}
			foreach (MagNode child in node.Children)
			{
				sb.Append(getInnerText(child));
			}
			return sb.ToString();
		}

		private enum StartEndToggle
		{
			Start, End, Toggle, Unknown
		}

		private static StartEndToggle parseStartEndToggle(MagNode markupNode)
		{
			string argument = getInnerText(markupNode).ToLower().Trim();
			switch (argument)
			{
				case "start":
					return StartEndToggle.Start;
				case "end":
					return StartEndToggle.End;
				case "":
					return StartEndToggle.Toggle;
				default:
					return StartEndToggle.Unknown;
			}
		}

		private void generateErrorMessage(NhInline nhTarget, string message)
		{
			EncountedError = true;
			using (NhSpan span = nhTarget.CreateSpan())
			{
				span.WriteClassAttr("error");
				span.WriteText(message);
			}
		}

		private void generateErrorMessageForBlock(NhBase nhTarget, string message)
		{
			if (nhTarget is NhBlock)
			{
				using (NhP p = ((NhBlock)nhTarget).CreateP())
				{
					generateErrorMessage(p, message);
				}
			}
			else if (nhTarget is NhUl)
			{
				using (NhLiInline li = ((NhUl)nhTarget).CreateLiInline())
				{
					generateErrorMessage(li, message);
				}
			}
			else if (nhTarget is NhOl)
			{
				using (NhLiInline li = ((NhOl)nhTarget).CreateLiInline())
				{
					generateErrorMessage(li, message);
				}
			}
		}

		private string createPreText(string src)
		{
			return src.Replace(" ", "\u00a0");
		}

		private class nestCounter
		{
			private int count = 0;
			public void NestIn()
			{
				count++;
			}
			public void NestOut()
			{
				count--;
			}
			public bool IsNested()
			{
				return count != 0;
			}
		}

		private class nestHandler : IDisposable
		{
			private nestCounter counter;
			public nestHandler(nestCounter counter, NhInline nhTarget, MagNode magNode, MagXHTMLGenerator parent)
			{
				this.counter = counter;
				if (counter.IsNested())
				{
					parent.generateErrorMessage(nhTarget, magNode.Value + "マークアップはネストできません。");
				}
				counter.NestIn();
			}
			public void Dispose()
			{
				counter.NestOut();
			}
		}

		private nestCounter underlineNestCount = new nestCounter();
		private void generateUnderlineMarkup(NhInline nhTarget, MagNode magNode)
		{
			using (nestHandler handler = new nestHandler(underlineNestCount, nhTarget, magNode, this))
			{
				using (NhSpan span = nhTarget.CreateSpan())
				{
					span.WriteClassAttr("generalUnderlined");
					walkInlineNodes(span, magNode);
				}
			}
		}

		private nestCounter hideNestCount = new nestCounter();
		private void generateHideMarkup(NhInline nhTarget, MagNode magNode)
		{
			using (nestHandler handler = new nestHandler(hideNestCount, nhTarget, magNode, this))
			{
				using (NhSpan span = nhTarget.CreateSpan())
				{
					span.WriteClassAttr("generalHide");
					walkInlineNodes(span, magNode);
				}
			}
		}

		private nestCounter quoteNestCount = new nestCounter();
		private void generateQuoteMarkup(NhInline nhTarget, MagNode magNode)
		{
			using (nestHandler handler = new nestHandler(quoteNestCount, nhTarget, magNode, this))
			{
				using (NhQ span = nhTarget.CreateQ())
				{
					walkInlineNodes(span, magNode);
				}
			}
		}

		private nestCounter strongNestCount = new nestCounter();
		private void generateStrongMarkup(NhInline nhTarget, MagNode magNode)
		{
			using (nestHandler handler = new nestHandler(strongNestCount, nhTarget, magNode, this))
			{
				using (NhStrong strong = nhTarget.CreateStrong())
				{
					walkInlineNodes(strong, magNode);
				}
			}
		}

		private nestCounter emphasisNestCount = new nestCounter();
		private void generateEmphasisMarkup(NhInline nhTarget, MagNode magNode)
		{
			using (nestHandler handler = new nestHandler(emphasisNestCount, nhTarget, magNode, this))
			{
				using (NhEm em = nhTarget.CreateEm())
				{
					walkInlineNodes(em, magNode);
				}
			}
		}

		private nestCounter strikeNestCount = new nestCounter();
		private void generateStrikeOutMarkup(NhInline nhTarget, MagNode magNode)
		{
			using (nestHandler handler = new nestHandler(strikeNestCount, nhTarget, magNode, this))
			{
				using (NhSpan span = nhTarget.CreateSpan())
				{
					span.WriteClassAttr("strikeout");
					walkInlineNodes(span, magNode);
				}
			}
		}

		private bool checkFirstChildText(NhInline nhTarget, MagNode magNode)
		{
			if (magNode.Children.Length == 0)
			{
				generateErrorMessage(nhTarget,
					magNode.Value + "マークアップの内容がありません。");
				return false;
			}
			if (magNode.Children[0].Type != EMagNodeType.Text)
			{
				generateErrorMessage(nhTarget,
					magNode.Value + "マークアップの内容は、URLから始める必要があります。");
				return false;
			}
			return true;
		}

		private int skipUntilSpaceCharacter(string src, int p)
		{
			while (true)
			{
				if (p >= src.Length) break;
				if (MagTokenizer.IsSpaceCharacter(src[p])) break;
				p++;
			}
			return p;
		}

		private int skipUntilNotSpaceCharacter(string src, int p)
		{
			while (true)
			{
				if (p >= src.Length) break;
				if (!MagTokenizer.IsSpaceCharacter(src[p])) break;
				p++;
			}
			return p;
		}

		private nestCounter anchorNestCount = new nestCounter();
		private void generateAnchorMarkup(NhInline nhTarget, MagNode magNode, bool external)
		{
			if (!checkFirstChildText(nhTarget, magNode)) return;

			string src = magNode.Children[0].Value;

			int p = skipUntilSpaceCharacter(src, 0);
			string url = src.Substring(0, p);

			using (nestHandler handler = new nestHandler(anchorNestCount, nhTarget, magNode, this))
			{
				using (NhA a = nhTarget.CreateA())
				{
					a.WriteAttribute("href", url);
					if (external || preview)
					{
						a.WriteAttribute("target", "_blank");
					}
					walkInlineNodes(a, magNode, skipUntilNotSpaceCharacter(src, p));
				}
			}
		}

		private void generateColorMarkup(NhInline nhTarget, MagNode magNode)
		{
			if (!checkFirstChildText(nhTarget, magNode)) return;

			string src = magNode.Children[0].Value;

			int p = skipUntilSpaceCharacter(src, 0);
			string colorName = src.Substring(0, p);

			using (NhSpan span = nhTarget.CreateSpan())
			{
				span.WriteAttribute("style", "color: " + colorName + ";");
				walkInlineNodes(span, magNode, skipUntilNotSpaceCharacter(src, p));
			}
		}

		// WikiPediaのURLエンコードが特殊である問題に対処するための専用エンコーダ
		private static string wikiPediaUrlEncode(string src)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(src);
			foreach (byte b in bytes)
			{
				if (b == 0x20)  // この条件が特異らしい
				{
					sb.Append('_');
				}
				else if (b == '/')  // この条件が特異らしい
				{
					sb.Append('/');
				}
				else if (WriteLib.IsUnreserved(b))
				{
					sb.Append((char)b);
				}
				else
				{
					sb.AppendFormat("%{0:X2}", b);
				}
			}

			return sb.ToString();
		}

		private void generateWikiPediaMarkup(NhInline nhTarget, MagNode magNode)
		{
			if (magNode.Children.Length == 0)
			{
				nhTarget.WriteAText("https://ja.wikipedia.org/", "WikiPedia");
			}
			else if (magNode.Children.Length == 1 && magNode.Children[0].Type == EMagNodeType.Text)
			{
				string keyword = magNode.Children[0].Value;
				string coockedName = wikiPediaUrlEncode(keyword);
				string url = string.Format("https://ja.wikipedia.org/wiki/{0}", coockedName);
				nhTarget.WriteAText(url, keyword);
			}
			else
			{
				generateErrorMessage(nhTarget,
					magNode.Value + "マークアップの内容にはシンプルなテキスト以外が記述でされています。");
				return;
			}
		}

		private void generateContentReferenceMarkup(NhInline nhTarget, MagNode magNode)
		{
			if (!checkFirstChildText(nhTarget, magNode)) return;

			string idString = magNode.Children[0].Value;
			ItemID itemID = new ItemID(idString);
			if (ContentFileAccessLayer.IsExist(itemID))
			{
				nhTarget.WriteAText("Content.modf?id=" + idString, ContentFileAccessLayer.GetItem(itemID).SubjectFull);
			}
			else
			{
				generateErrorMessage(nhTarget,
					magNode.Value + "マークアップの内容に指定された" + idString + "に対応するコンテンツは見つかりません。");
			}
		}

		private void generateSuperQMarkup(NhInline nhTarget, MagNode magNode)
		{
			string[] tokens = ParseCommaSeparatedArgument(nhTarget, magNode);
			if (tokens == null) return;
			generateErrorMessage(nhTarget, "超クイズは現在サポートされていません。(superqマークアップはobsoleteされました)");
			return;
		}

		private void generateFlashMarkup(NhInline nhTarget, MagNode magNode)
		{
			string[] tokens = ParseCommaSeparatedArgument(nhTarget, magNode);
			if (tokens == null) return;
			generateErrorMessage(nhTarget, "フラッシュは現在サポートされていません。(flashマークアップはobsoleteされました)");
			return;
		}

		private void generateImageMarkup(NhInline nhTarget, MagNode magNode)
		{
			if (!checkFirstChildText(nhTarget, magNode)) return;

			if (magNode.Children.Length > 1)
			{
				generateErrorMessage(nhTarget, "imgマークアップに解釈できない内容が含まれています。正しく終了マークアップ$$!が記述されているか確認して下さい。");
				return;
			}

			string src = magNode.Children[0].Value;
			int p = skipUntilSpaceCharacter(src, 0);

			int number;
			try
			{
				number = int.Parse(src.Substring(0, p));
			}
			catch (FormatException)
			{
				generateErrorMessage(nhTarget, src.Substring(0, p) + "は、画像番号として解釈できません。半角数字で記述する必要があります。");
				return;
			}

			generateErrorMessage(nhTarget, "imgマークアップはサポートされていません。");
		}

		private void generateInlineMarkups(NhInline nhTarget, MagNode magNode)
		{
			switch (magNode.Value)
			{
				case "_":
					generateUnderlineMarkup(nhTarget, magNode);
					break;
				case "*":
					generateStrongMarkup(nhTarget, magNode);
					break;
				case "/":
					generateEmphasisMarkup(nhTarget, magNode);
					break;
				case "-":
					generateStrikeOutMarkup(nhTarget, magNode);
					break;
				case "a":
					generateAnchorMarkup(nhTarget, magNode, false);
					break;
				case "ae":
					generateAnchorMarkup(nhTarget, magNode, true);
					break;
				case "img":
					generateImageMarkup(nhTarget, magNode);
					break;
				case "wikipedia":
					generateWikiPediaMarkup(nhTarget, magNode);
					break;
				case "ref":
					generateContentReferenceMarkup(nhTarget, magNode);
					break;
				case "xhide":
					generateHideMarkup(nhTarget, magNode);
					break;
				case "q":
					generateQuoteMarkup(nhTarget, magNode);
					break;
				case "cl":
					generateColorMarkup(nhTarget, magNode);
					break;
				case "flash":
					generateFlashMarkup(nhTarget, magNode);
					break;
				case "superq":
					generateSuperQMarkup(nhTarget, magNode);
					break;
				default:
					System.Diagnostics.Trace.Fail("内部エラー: " + magNode.Value + "は存在しないインライン マークアップの名前です。");
					break;
			}
		}

		private void generateInlineNodes(NhInline nhTarget, MagNode magNode, int numberOfDroppingFirstTextCharacters)
		{
			switch (magNode.Type)
			{
				case EMagNodeType.Text:
					string src = magNode.Value.Substring(numberOfDroppingFirstTextCharacters);
					if (preMode)
					{
						if (src.IndexOf('\t') >= 0)
						{
							generateErrorMessage(nhTarget, "Tab文字は使用できません。空白文字を使用して記述して下さい。");
						}
						nhTarget.WriteText(createPreText(src));
					}
					else
					{
						nhTarget.WriteText(src);
					}
					break;
				case EMagNodeType.NoCookText:
					goto case EMagNodeType.Text;
				case EMagNodeType.ErrorText:
					generateErrorMessage(nhTarget, magNode.Value);
					break;
				case EMagNodeType.InlineMarkup:
					generateInlineMarkups(nhTarget, magNode);
					break;
				default:
					System.Diagnostics.Trace.Fail("内部エラー: " + magNode.Type + "は出現してはならないノードタイプです。");
					break;
			}
		}

		private void walkInlineNodes(NhInline nhTarget, MagNode magNode, int numberOfDroppingFirstTextCharacters)
		{
			for (int i = 0; i < magNode.Children.Length; i++)
			{
				generateInlineNodes(nhTarget, magNode.Children[i],
					i == 0 ? numberOfDroppingFirstTextCharacters : 0);
			}
		}

		private void walkInlineNodes(NhInline nhTarget, MagNode magNode)
		{
			walkInlineNodes(nhTarget, magNode, 0);
		}

		private class nestInformation
		{
			public readonly NhBase ParentNhNode;
			public readonly MagNode TargetMagNode;
			public bool IsMatch(EMagNodeType targetType, string targetValue)
			{
				return TargetMagNode.Type == targetType
					&& TargetMagNode.Value == targetValue;
			}
			public nestInformation(NhBase parentNhNode, MagNode targetMagNode)
			{
				ParentNhNode = parentNhNode;
				TargetMagNode = targetMagNode;
			}
		}

		private class nestInformationStack
		{
			private ArrayList stack = new ArrayList();
			public void Push(nestInformation info)
			{
				stack.Insert(0, info);
			}
			public nestInformation Pop()
			{
				nestInformation item = (nestInformation)stack[0];
				stack.RemoveAt(0);
				return item;
			}
			public nestInformation Peek()
			{
				return (nestInformation)stack[0];
			}
			public bool hasBlockMarkup(string markupName)
			{
				foreach (nestInformation item in stack)
				{
					if (item.TargetMagNode.Type == EMagNodeType.BlockMarkup
						&& item.TargetMagNode.Value == markupName)
					{
						return true;
					}
				}
				return false;
			}
		}

		private ArrayList errorMessages = new ArrayList();
		private nestInformationStack parentNhNodeStack = new nestInformationStack();
		private nestInformation currentInfo;
		private bool preMode = false;

		private void commonBlockStartProcess(MagNode targetNode)
		{
			parentNhNodeStack.Push(currentInfo);
			if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "ul"))
			{
				currentInfo = new nestInformation(((NhUl)currentInfo.ParentNhNode).CreateLiBlock(),
					targetNode);
				parentNhNodeStack.Push(currentInfo);
			}
			else if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "ol"))
			{
				currentInfo = new nestInformation(((NhOl)currentInfo.ParentNhNode).CreateLiBlock(),
					targetNode);
				parentNhNodeStack.Push(currentInfo);
			}
		}

		private void commonBlockEndProcess(string targetName)
		{
			if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, targetName))
			{
				((IDisposable)currentInfo.ParentNhNode).Dispose();
				if (parentNhNodeStack.Peek().ParentNhNode is NhLiBlock)
				{
					NhLiBlock liPopped = (NhLiBlock)parentNhNodeStack.Pop().ParentNhNode;
					liPopped.Dispose();
				}
				currentInfo = parentNhNodeStack.Pop();
			}
			else
			{
				errorMessages.Add("対応する開始" + targetName + "マークアップのない終了"
					+ targetName + "マークアップが発見されました。");
			}
		}

		private void startEndToggleError(MagNode targetNode)
		{
			errorMessages.Add(getInnerText(targetNode) + "は無効です。start, end または無しでなければなりません。");
		}

		private void generateHx(int level, MagNode targetNode)
		{
			int hash = targetNode.Children[0].Value.GetHashCode();
			string fragmentID = "h" + hash.ToString("x");
			using (NhHx hx = ((NhBlock)currentInfo.ParentNhNode).CreateHx(level))
			{
#if ENABLE_ANCHOR
				hx.WriteIDAttr(fragmentID);
#endif
				hx.WriteText(targetNode.Children[0].Value);
#if ENABLE_ANCHOR
				hx.WriteText(" ");
				using (NhA a = hx.CreateA())
				{
					a.WriteClassAttr("anchorHere");
					a.WriteAttribute("href", "#" + fragmentID);
					a.WriteText("§");
				}
#endif
			}
		}

		private void generateUl(MagNode targetNode)
		{
			switch (parseStartEndToggle(targetNode))
			{
				case StartEndToggle.Start:
					commonBlockStartProcess(targetNode);
					currentInfo = new nestInformation(((NhBlock)currentInfo.ParentNhNode).CreateUl(), targetNode);
					break;
				case StartEndToggle.End:
					commonBlockEndProcess("ul");
					break;
				case StartEndToggle.Toggle:
					if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "ul"))
					{
						goto case StartEndToggle.End;
					}
					else
					{
						goto case StartEndToggle.Start;
					}
				case StartEndToggle.Unknown:
					startEndToggleError(targetNode);
					break;
			}
		}

		private void generateOl(MagNode targetNode)
		{
			switch (parseStartEndToggle(targetNode))
			{
				case StartEndToggle.Start:
					commonBlockStartProcess(targetNode);
					currentInfo = new nestInformation(((NhBlock)currentInfo.ParentNhNode).CreateOl(), targetNode);
					break;
				case StartEndToggle.End:
					commonBlockEndProcess("ol");
					break;
				case StartEndToggle.Toggle:
					if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "ol"))
					{
						goto case StartEndToggle.End;
					}
					else
					{
						goto case StartEndToggle.Start;
					}
				case StartEndToggle.Unknown:
					startEndToggleError(targetNode);
					break;
			}
		}

		private void generateIndent(MagNode targetNode)
		{
			switch (parseStartEndToggle(targetNode))
			{
				case StartEndToggle.Start:
					commonBlockStartProcess(targetNode);
					currentInfo = new nestInformation(((NhBlock)currentInfo.ParentNhNode).CreateDiv(),
						targetNode);
					currentInfo.ParentNhNode.WriteClassAttr("generalIndent");
					break;
				case StartEndToggle.End:
					commonBlockEndProcess(">");
					break;
				case StartEndToggle.Toggle:
					if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, ">"))
					{
						goto case StartEndToggle.End;
					}
					else
					{
						goto case StartEndToggle.Start;
					}
				case StartEndToggle.Unknown:
					startEndToggleError(targetNode);
					break;
			}
		}

		private void generateBlockQuote(MagNode targetNode)
		{
			switch (parseStartEndToggle(targetNode))
			{
				case StartEndToggle.Start:
					commonBlockStartProcess(targetNode);
					currentInfo = new nestInformation(((NhBlock)currentInfo.ParentNhNode).CreateBlockQuote(),
						targetNode);
					break;
				case StartEndToggle.End:
					commonBlockEndProcess("bq");
					break;
				case StartEndToggle.Toggle:
					if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "bq"))
					{
						goto case StartEndToggle.End;
					}
					else
					{
						goto case StartEndToggle.Start;
					}
				case StartEndToggle.Unknown:
					startEndToggleError(targetNode);
					break;
			}
		}

		private void generateLargeText(MagNode targetNode)
		{
			switch (parseStartEndToggle(targetNode))
			{
				case StartEndToggle.Start:
					commonBlockStartProcess(targetNode);
					NhDiv div = ((NhBlock)currentInfo.ParentNhNode).CreateDiv();
					div.WriteClassAttr("largeText");
					currentInfo = new nestInformation(div, targetNode);
					break;
				case StartEndToggle.End:
					commonBlockEndProcess("ltx");
					break;
				case StartEndToggle.Toggle:
					if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "ltx"))
					{
						goto case StartEndToggle.End;
					}
					else
					{
						goto case StartEndToggle.Start;
					}
				case StartEndToggle.Unknown:
					startEndToggleError(targetNode);
					break;
			}
		}

		private void generatePre(MagNode targetNode)
		{
			switch (parseStartEndToggle(targetNode))
			{
				case StartEndToggle.Start:
					if (!checkPreMarkupCanNotBeNested()) break;
					commonBlockStartProcess(targetNode);
					currentInfo = new nestInformation(((NhBlock)currentInfo.ParentNhNode).CreateDiv(),
						targetNode);
					currentInfo.ParentNhNode.WriteClassAttr("generalMonospace");
					break;
				case StartEndToggle.End:
					commonBlockEndProcess("pre");
					break;
				case StartEndToggle.Toggle:
					if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "pre"))
					{
						goto case StartEndToggle.End;
					}
					else
					{
						goto case StartEndToggle.Start;
					}
				case StartEndToggle.Unknown:
					startEndToggleError(targetNode);
					break;
			}
		}

		private bool tableStartedNow = false;
		private void generateTable(MagNode targetNode)
		{
			switch (parseStartEndToggle(targetNode))
			{
				case StartEndToggle.Start:
					if (!checkBlockMarkupCanNotBeInTableMarkup(targetNode)) return;
					commonBlockStartProcess(targetNode);
					currentInfo = new nestInformation(((NhBlock)currentInfo.ParentNhNode).CreateTable(),
						targetNode);
					tableStartedNow = true;
					break;
				case StartEndToggle.End:
					commonBlockEndProcess("table");
					break;
				case StartEndToggle.Toggle:
					if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "table"))
					{
						goto case StartEndToggle.End;
					}
					else
					{
						goto case StartEndToggle.Start;
					}
				case StartEndToggle.Unknown:
					startEndToggleError(targetNode);
					break;
			}
		}

		private void writeRawCrLf()
		{
			currentInfo.ParentNhNode.WriteRawString("\r\n");
		}

		private void writeRawStartElementWithoutCrLf(string elementName)
		{
			currentInfo.ParentNhNode.WriteRawString("<");
			currentInfo.ParentNhNode.WriteRawString(elementName);
			currentInfo.ParentNhNode.WriteRawString(">");
		}

		private void writeRawStartElement(string elementName)
		{
			writeRawStartElementWithoutCrLf(elementName);
			writeRawCrLf();
		}

		private void writeRawEndElement(string elementName)
		{
			currentInfo.ParentNhNode.WriteRawString("</");
			currentInfo.ParentNhNode.WriteRawString(elementName);
			currentInfo.ParentNhNode.WriteRawString(">\r\n");
		}

		private void writeRawElementString(string elementName, string val)
		{
			writeRawStartElementWithoutCrLf(elementName);
			currentInfo.ParentNhNode.WriteRawString(XmlUtility.EscapeStringByPredefinedEntities(val));
			writeRawEndElement(elementName);
		}

		private void writeRawSizeElement(int width, int height, bool suggested)
		{
			currentInfo.ParentNhNode.WriteRawString("<it:size width=\"");
			currentInfo.ParentNhNode.WriteRawString(width.ToString());
			currentInfo.ParentNhNode.WriteRawString("\" height=\"");
			currentInfo.ParentNhNode.WriteRawString(height.ToString());
			if (suggested)
			{
				currentInfo.ParentNhNode.WriteRawString("\" suggestedSize=\"true");
			}
			currentInfo.ParentNhNode.WriteRawString("\" />\r\n");
		}

		private void generateTableRaw(NhTable table, MagNode node, bool firstLine)
		{
			using (NhTr tr = table.CreateTr())
			{
				int p = 0;
				string leftText = "";
				while (true)
				{
					if (p >= node.Children.Length && leftText.Length == 0) break;
					using (NhInline thOrTd = firstLine ? (NhInline)tr.CreateThInline() : (NhInline)tr.CreateTdInline())
					{
						while (true)
						{
							if (leftText.Length > 0)
							{
								int commmaPos = leftText.IndexOf(',');
								if (commmaPos >= 0)
								{
									thOrTd.WriteText(leftText.Substring(0, commmaPos));
									leftText = leftText.Substring(commmaPos + 1);
									break;
								}
								thOrTd.WriteText(leftText);
								leftText = "";
							}

							if (p >= node.Children.Length) break;
							if (node.Children[p].Type == EMagNodeType.NoCookText)
							{
								thOrTd.WriteText(leftText);
								thOrTd.WriteText(node.Children[p].Value);
								leftText = "";
							}
							else if (node.Children[p].Type == EMagNodeType.Text)
							{
								leftText = node.Children[p].Value;
							}
							else
							{
								generateInlineNodes(thOrTd, node.Children[p], 0);
							}
							p++;
						}
					}
				}
			}
		}

		private void generateYouTube(NhBase nhbase, MagNode magNode)
		{
			if (magNode.Children.Length == 0)
			{
				generateErrorMessageForBlock(nhbase, "xytマークアップの内容が指定されていません。");
				return;
			}

			string src = magNode.Children[0].Value;

			int p = skipUntilSpaceCharacter(src, 0);

			string youTubeID = src.Substring(0, p);

			int p2 = skipUntilNotSpaceCharacter(src, p);
			int p3 = skipUntilSpaceCharacter(src, p2);

			int width = 425;
			if (p2 != p3)
			{
				try
				{
					width = int.Parse(src.Substring(p2, p3 - p2));
				}
				catch (FormatException)
				{
					generateErrorMessageForBlock(nhbase, src.Substring(p2, p3 - p2) + "は、画像幅サイズとして解釈できません。半角数字で記述する必要があります。");
					return;
				}
				if (width <= 0)
				{
					generateErrorMessageForBlock(nhbase, src.Substring(p2, p3 - p2) + "は、画像幅サイズとして解釈できません。半角数字で記述する必要があります。");
					return;
				}
			}

			int p4 = skipUntilNotSpaceCharacter(src, p3);
			int p5 = skipUntilSpaceCharacter(src, p4);

			int height = 350;
			if (p4 != p5)
			{
				try
				{
					height = int.Parse(src.Substring(p4, p5 - p4));
				}
				catch (FormatException)
				{
					generateErrorMessageForBlock(nhbase, src.Substring(p4, p5 - p4) + "は、画像高さサイズとして解釈できません。半角数字で記述する必要があります。");
					return;
				}
				if (height <= 0)
				{
					generateErrorMessageForBlock(nhbase, src.Substring(p4, p5 - p4) + "は、画像高さサイズとして解釈できません。半角数字で記述する必要があります。");
					return;
				}
			}

			string remain = src.Substring(p5);
			if (remain.Length > 0)
			{
				generateErrorMessageForBlock(nhbase, remain + "は、xytマークアップの内容として解釈できません。");
				return;
			}

			const string formatString = "<div class=\"youtube\">"
				+ "<iframe width=\"{1}\" height=\"{2}\" src=\"https://www.youtube.com/embed/{0}\" frameborder=\"0\" allowfullscreen=\"allowfullscreen\"></iframe>"
				+ "</div>";

			nhbase.WriteRawString(string.Format(formatString, youTubeID, width, height));
		}

		private void generateAsinReference(MagNode targetNode)
		{
			EmbeddedAsins.Add(getInnerText(targetNode).Trim());
		}

		private void generateRepresentAsinReference(MagNode targetNode)
		{
			RepresentAsins.Add(getInnerText(targetNode).Trim());
		}

		/// <summary>
		/// カンマ区切りの内容を"$$,"を例外扱いにして分割
		/// </summary>
		private string[] ParseCommaSeparatedArgument(NhBase nhbase, MagNode magNode)
		{
			ArrayList result = new ArrayList();
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (MagNode child in magNode.Children)
			{
				if (child.Type == EMagNodeType.Text)
				{
					for (int i = 0; i < child.Value.Length; i++)
					{
						if (child.Value[i] == ',')
						{
							result.Add(sb.ToString());
							sb.Length = 0;
						}
						else
						{
							sb.Append(child.Value[i]);
						}
					}
				}
				else if (child.Type == EMagNodeType.NoCookText)
				{
					sb.Append(child.Value);
				}
				else
				{
					generateErrorMessageForBlock(nhbase, "gm/gmp/gmlマークアップに解釈できない内容が含まれます。");
					return null;
				}
			}
			result.Add(sb.ToString());
			return (string[])result.ToArray(typeof(string));
		}

		private static int uniqueCounter = 0;
		private int getUniqueNumber()
		{
			lock (this)
			{
				return uniqueCounter++;
			}
		}

		private string escapeForJS(string s)
		{
			return XmlUtility.EscapeStringByPredefinedEntities(s).Replace("\"", "\u0022");
		}

		private enum GoogleMapsType
		{
			Normal, Point, PolyLine
		}

		[Obsolete]
		private void generateGoogleMaps(NhBase nhbase, MagNode magNode, GoogleMapsType googleMapsType)
		{
			string[] tokens = ParseCommaSeparatedArgument(nhbase, magNode);
			if (tokens == null) return;

			if (googleMapsType == GoogleMapsType.Point)
			{
				if (tokens.Length != 6)
				{
					generateErrorMessageForBlock(nhbase, "gmpマークアップには6つの引数が必要です。");
					return;
				}
			}
			else
			{
				if (tokens.Length < 6)
				{
					generateErrorMessageForBlock(nhbase, "gm/gmlマークアップには少なくとも6つの引数が必要です。");
					return;
				}
			}

			if ((tokens.Length - 6) % 4 != 0)
			{
				generateErrorMessageForBlock(nhbase, "gm/gmlマークアップは6+4*n個の引数が必要です。");
				return;
			}

			// 地図タイプの解析
			string mapType = "";
			switch (tokens[5].Trim())
			{
				case "a": mapType = @"google.maps.MapTypeId.SATELLITE"; break;
				case "h": mapType = @"google.maps.MapTypeId.HYBRID"; break;
				case "r": mapType = @"google.maps.MapTypeId.ROADMAP"; break;
				case "": goto case "r";
				default:
					generateErrorMessageForBlock(nhbase, tokens[5] + "は解釈できない地図タイプです。");
					return;
			}

			// ズーム倍率の確定
			string zoom = tokens[4].Trim().Length == 0 ? "13" : tokens[4];

			// ヘッダでGoogle Mapsのスクリプトを読み込むことをリクエスト
			nhbase.WriteRawString("<it:prototypeJsRequied />");
			nhbase.WriteRawString("<it:googleMapsRequied />");

			// ページ内でユニークなIDを生成する
			string idName = "gm" + getUniqueNumber().ToString();
			string mapVarName = "map_gm" + getUniqueNumber().ToString();

			nhbase.WriteRawString("<div class='gmframe'>");
			nhbase.WriteRawString("<div id='");
			nhbase.WriteRawString(idName);
			nhbase.WriteRawString("' style='position:relative; width:");
			nhbase.WriteRawString(tokens[0].Trim().Length == 0 ? "100%" : tokens[0]);
			nhbase.WriteRawString("; height:");
			nhbase.WriteRawString(tokens[1].Trim().Length == 0 ? "20em" : tokens[1]);
			nhbase.WriteRawString(";'></div>\r\n");
			nhbase.WriteRawString(@"<script type='text/javascript'>
var ");
			nhbase.WriteRawString(mapVarName);

			if (googleMapsType == GoogleMapsType.Point)
			{
				int index = 0;
				for (int i = 6; i < tokens.Length; i += 4)
				{
					nhbase.WriteRawString(@",");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_m");
					nhbase.WriteRawString(index.ToString());
					nhbase.WriteRawString(@",");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_h");
					nhbase.WriteRawString(index.ToString());
				}
			}
			nhbase.WriteRawString(@";");
			nhbase.WriteRawString(@"
$( document ).ready( function()
{
if (GBrowserIsCompatible()) {
var myOptions");
			nhbase.WriteRawString(mapVarName);
			nhbase.WriteRawString(@"= {
    center: new google.maps.LatLng(");
			nhbase.WriteRawString(tokens[2]);
			nhbase.WriteRawString(@",");
			nhbase.WriteRawString(tokens[3]);
			nhbase.WriteRawString(@"),
    zoom: ");
			nhbase.WriteRawString(zoom);
			nhbase.WriteRawString(@",
    mapTypeId: ");
			nhbase.WriteRawString(mapType);
			nhbase.WriteRawString(@",
    // Add controls
    mapTypeControl: true,
    scaleControl: true,
    overviewMapControl: true,
    overviewMapControlOptions: {
      opened: true
    }
};
");
			nhbase.WriteRawString(mapVarName);
			nhbase.WriteRawString(@" = new google.maps.Map(document.getElementById('");
			nhbase.WriteRawString(idName);
			nhbase.WriteRawString(@"'),myOptions");
			nhbase.WriteRawString(mapVarName);
			nhbase.WriteRawString(@");");

			if (googleMapsType == GoogleMapsType.Point)
			{
				nhbase.WriteRawString(@"var latlng = new google.maps.LatLng(");
				nhbase.WriteRawString(tokens[2]);
				nhbase.WriteRawString(@",");
				nhbase.WriteRawString(tokens[3]);
				nhbase.WriteRawString(@"); new google.maps.Marker({position: latlng, map:" + mapVarName + "});");
			}
			else
			{
				int index = 0;
				for (int i = 6; i < tokens.Length; i += 4)
				{
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_h");
					nhbase.WriteRawString(index.ToString());
					nhbase.WriteRawString(" = '<span class=\"gmfukidasi\">");
					if (tokens[i + 3].Trim().Length > 0)
					{
						nhbase.WriteRawString("<a class=\"gmfukidasi\" href=\"");
						nhbase.WriteRawString(escapeForJS(tokens[i + 3]));
						nhbase.WriteRawString("\" target=\"_blank\">");
						nhbase.WriteRawString(escapeForJS(tokens[i + 2]));
						nhbase.WriteRawString(@"</a>");
					}
					else
					{
						nhbase.WriteRawString(XmlUtility.EscapeStringByPredefinedEntitiesIncludingQuote(tokens[i + 2]));
					}
					nhbase.WriteRawString("</span>';");

					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_m");
					nhbase.WriteRawString(index.ToString());
					nhbase.WriteRawString(@" = createMarker(");
					nhbase.WriteRawString(@"new google.maps.LatLng(");
					nhbase.WriteRawString(tokens[i]);
					nhbase.WriteRawString(@",");
					nhbase.WriteRawString(tokens[i + 1]);
					nhbase.WriteRawString(@")");
					nhbase.WriteRawString(@", ");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_h");
					nhbase.WriteRawString(index.ToString());
					nhbase.WriteRawString(@", ");
					nhbase.WriteRawString(index.ToString());
					nhbase.WriteRawString(@", ");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@");");
					index++;
				}
				if (googleMapsType == GoogleMapsType.PolyLine)
				{
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_p = new google.maps.Polyline({path:[");
					for (int i = 6; i < tokens.Length; i += 4)
					{
						nhbase.WriteRawString(@"new google.maps.LatLng(");
						nhbase.WriteRawString(tokens[i]);
						nhbase.WriteRawString(@",");
						nhbase.WriteRawString(tokens[i + 1]);
						nhbase.WriteRawString(@"),");
					}
					nhbase.WriteRawString("], strokeColor:'#ff0000', strokeOpacity:0.5, strokeWeight: 10, geodesic: true });");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString("_p.setMap(");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(");");
				}
			}
			nhbase.WriteRawString(@"}});
</script>
");
			// センタリングUI
			nhbase.WriteRawString("<ul class='gmlist'>");
			nhbase.WriteRawString("<li><a href='javascript:");
			nhbase.WriteRawString(mapVarName);
			nhbase.WriteRawString(".setCenter(new google.maps.LatLng(");
			nhbase.WriteRawString(tokens[2]);
			nhbase.WriteRawString(@",");
			nhbase.WriteRawString(tokens[3]);
			nhbase.WriteRawString(@"));'>最初の位置に戻す</a> [<a href='javascript:");
			nhbase.WriteRawString(mapVarName);
			nhbase.WriteRawString(".setCenter(new google.maps.LatLng(");
			nhbase.WriteRawString(tokens[2]);
			nhbase.WriteRawString(@",");
			nhbase.WriteRawString(tokens[3]);
			nhbase.WriteRawString(@"),");
			nhbase.WriteRawString(zoom);
			nhbase.WriteRawString(@",");
			nhbase.WriteRawString(mapType);
			nhbase.WriteRawString(@");");
			nhbase.WriteRawString(mapVarName);
			nhbase.WriteRawString(@".setZoom(");
			nhbase.WriteRawString(tokens[4]);
			nhbase.WriteRawString(@");'>地図をリセット</a>]</li>");

			if (googleMapsType != GoogleMapsType.Point)
			{
				int index = 0;
				for (int i = 6; i < tokens.Length; i += 4)
				{
					nhbase.WriteRawString("<li><a href='javascript:clickedMarker(");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_m");
					nhbase.WriteRawString(index.ToString());
					nhbase.WriteRawString(@",");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@"_h");
					nhbase.WriteRawString(index.ToString());
					nhbase.WriteRawString(@",");
					nhbase.WriteRawString(mapVarName);
					nhbase.WriteRawString(@");'>");
					nhbase.WriteRawString(((char)('A' + index)).ToString());
					nhbase.WriteRawString(@". ");
					nhbase.WriteRawString(escapeForJS(tokens[i + 2]));
					nhbase.WriteRawString(@"</a></li>");
					index++;
				}
			}
			nhbase.WriteRawString("</ul>");
			nhbase.WriteRawString("</div>");
		}


		private void generateLeafletMaps(NhBase nhbase, MagNode magNode, GoogleMapsType googleMapsType)
		{
			string[] tokens = ParseCommaSeparatedArgument(nhbase, magNode);
			if (tokens == null) return;

			if (googleMapsType == GoogleMapsType.Point)
			{
				if (tokens.Length != 6)
				{
					generateErrorMessageForBlock(nhbase, "gmpマークアップには6つの引数が必要です。");
					return;
				}
			}
			else
			{
				if (tokens.Length < 6)
				{
					generateErrorMessageForBlock(nhbase, "gm/gmlマークアップには少なくとも6つの引数が必要です。");
					return;
				}
			}

			if ((tokens.Length - 6) % 4 != 0)
			{
				generateErrorMessageForBlock(nhbase, "gm/gmlマークアップは6+4*n個の引数が必要です。");
				return;
			}

			// 地図タイプの解析
			string mapType = tokens[5].Trim();
			if (string.IsNullOrWhiteSpace(mapType)) mapType = "r";
			if (mapType.Length != 1)
			{
				generateErrorMessageForBlock(nhbase, tokens[5] + "は解釈できない地図タイプです。");
				return;
			}

			// ズーム倍率の確定
			string zoom = tokens[4].Trim().Length == 0 ? "13" : tokens[4];

			// ヘッダでGoogle Mapsのスクリプトを読み込むことをリクエスト
			nhbase.WriteRawString("<it:leafletRequied />");

			// ページ内でユニークなIDを生成する
			string idName = "gm" + getUniqueNumber().ToString();

			nhbase.WriteRawString("<div id='");
			nhbase.WriteRawString(idName);
			nhbase.WriteRawString("'></div>\r\n");

			nhbase.WriteRawString(@"<script type='text/javascript'>
var t = function (event) {
createGM('");
			nhbase.WriteRawString(idName);
			nhbase.WriteRawString("','");
			nhbase.WriteRawString(mapType);
			nhbase.WriteRawString("','");
			nhbase.WriteRawString(tokens[0]);   // xSize
			nhbase.WriteRawString("','");
			nhbase.WriteRawString(tokens[1]);   // ySize
			nhbase.WriteRawString("','");
			nhbase.WriteRawString(tokens[2]);   // lat
			nhbase.WriteRawString("','");
			nhbase.WriteRawString(tokens[3]);   // lon
			nhbase.WriteRawString("',");
			nhbase.WriteRawString(zoom);
			nhbase.WriteRawString(",[");
			for (int i = 6; i < tokens.Length; i += 4)
			{
				nhbase.WriteRawString("{ latitude:");
				nhbase.WriteRawString(tokens[i]);
				nhbase.WriteRawString(",longitude:");
				nhbase.WriteRawString(tokens[i + 1]);
				nhbase.WriteRawString(",explanation:'");
				nhbase.WriteRawString(tokens[i + 2]);
				nhbase.WriteRawString("',url:'");
				nhbase.WriteRawString(tokens[i + 3]);
				nhbase.WriteRawString("'},");
			}
			nhbase.WriteRawString("],");
			nhbase.WriteRawString(googleMapsType == GoogleMapsType.PolyLine ? "true" : "false");
			nhbase.WriteRawString(@");}
$(document).on('pageinit', t);
allInitializers.push(t);
");

			nhbase.WriteRawString("</script>");
		}

		// ModulaFModule.csのwriteInterimKeywordの文字列生成版
		private static string writeInterimKeyword(FullKeyword keyword)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append("<it:keyword>");

			for (int depth = 0; depth < keyword.Keywords.Length; depth++)
			{
				string[] targetNames = new string[depth + 1];
				for (int i = 0; i < depth + 1; i++)
				{
					targetNames[i] = keyword.Keywords[i];
				}
				FullKeyword targetKeyword = new FullKeyword(targetNames);

				SectionDef targetDef = SectionDefManager.GetSectionDef(targetKeyword);
				sb.Append("<it:item code='");
                System.Text.StringBuilder stringBuilder = sb.Append(HttpUtility.HtmlEncode(targetDef.ShortKeyword.Trim()));
				sb.Append("' class='normal'>");

				sb.Append("<it:title>");
				sb.Append(HttpUtility.HtmlEncode(keyword.Keywords[depth]));
				sb.Append("</it:title>");

				//if( !itemID.IsNoItem() )
				//{
				//	ItemID prevKWItemID = ItemListManager.ContentList.GetPrevItemIDByKeyword(itemID,targetKeyword);
				//	writeNextPrevItem( writer, prevKWItemID, "prev" );

				//	ItemID nextKWItemID = ItemListManager.ContentList.GetNextItemIDByKeyword(itemID,targetKeyword);
				//	writeNextPrevItem( writer, nextKWItemID, "next" );
				//}

				// Todo: キーワードのサマリ情報を追加 
				// (構造を持ちWriteElementStringでは済まないかもしれない)
				// writer.WriteElementString("summary",XmlNamespaces.Interim,"サマリ");

				sb.Append("</it:item>");
			}

			sb.Append("</it:keyword>");
			return sb.ToString();
		}

		private void generateContentTree(MagNode magNode)
		{
			string shortName = "";
			if (magNode.Children.Length > 0)
			{
				shortName = magNode.Children[0].Value;
			}

			if (currentInfo.ParentNhNode is NhUl || currentInfo.ParentNhNode is NhOl)
			{
				generateErrorMessageForBlock(currentInfo.ParentNhNode, "ul/olマークアップの中でctreeマークアップを使うことはできません。");
				return;
			}

			if (shortName.Length > 0 && !ShortKeywordFinder.IsShortKeyword(shortName))
			{
				generateErrorMessageForBlock(currentInfo.ParentNhNode, shortName + "はキーワードの短い名前ではありません。");
				return;
			}

			using (NhP p = ((NhBlock)currentInfo.ParentNhNode).CreateP())
			{
				generateErrorMessage(p, "ctreeはサポートされていません。");
			}
		}

		private void generateXhtmlFragment(MagNode magNode)
		{
			var doc = new System.Xml.XmlDocument();
			try
			{
				doc.LoadXml("<div>" + magNode.Value + "</div>");
			}
			catch (System.Xml.XmlException e)
			{
				using (NhP p = ((NhBlock)currentInfo.ParentNhNode).CreateP())
				{
					this.generateErrorMessage(p, "xhtmlマークアップの内容がXML文書フラグメントとして解釈できません。参考情報: " + e.Message);
				}
				return;
			}

			if (preview)
			{
				currentInfo.ParentNhNode.WriteRawString(magNode.Value);
			}
			else
			{
				currentInfo.ParentNhNode.WriteRawString("<it:xhtml>");
				currentInfo.ParentNhNode.WriteRawString(HttpUtility.HtmlEncode(magNode.Value));
				currentInfo.ParentNhNode.WriteRawString("</it:xhtml>");
			}
		}

		private bool checkOnlyOneBlockMarkupMustBeInSingleLine(MagNode node)
		{
			if (node.Children.Length == 1 && node.Children[0].Type == EMagNodeType.BlockMarkup) return true;
			foreach (MagNode child in node.Children)
			{
				if (child.Type == EMagNodeType.BlockMarkup)
				{
					string errorMessage = child.Value + "マークアップは、1行内で前後に他のものを記述することはできません。";
					NhBase nhbase = currentInfo.ParentNhNode;
					// ここは他のオブジェクトが来る可能性があるので、要再チェック
					if (nhbase is NhUl)
					{
						using (NhLiInline li = ((NhUl)nhbase).CreateLiInline())
						{
							this.generateErrorMessage(li, errorMessage);
						}
					}
					else if (nhbase is NhOl)
					{
						using (NhLiInline li = ((NhOl)nhbase).CreateLiInline())
						{
							this.generateErrorMessage(li, errorMessage);
						}
					}
					else
					{
						using (NhP p = ((NhBlock)nhbase).CreateP())
						{
							this.generateErrorMessage(p, errorMessage);
						}
					}
					return false;
				}
			}
			return true;
		}

		private bool checkBlockMarkupCanNotBeInTableMarkup(MagNode node)
		{
			if (currentInfo.IsMatch(EMagNodeType.BlockMarkup, "table"))
			{
				using (NhTr tr = ((NhTable)currentInfo.ParentNhNode).CreateTr())
				{
					using (NhTdInline td = tr.CreateTdInline())
					{
						this.generateErrorMessage(td, "tableマークアップの範囲内に、" + node.Value + "マークアップを記述することはできません。");
					}
				}
				return false;
			}
			return true;
		}

		private bool checkHeadingMarkupMustHaveOnlyText(MagNode node)
		{
			if (node.Children.Length == 1 && node.Children[0].Type == EMagNodeType.Text) return true;
			generateErrorMessageForBlock(currentInfo.ParentNhNode, node.Value + "マークアップ内には1文字以上のテキストのみを記述できます。");
			return false;
		}

		private bool checkParentMustBlockMarkup(MagNode node)
		{
			if (currentInfo.ParentNhNode is NhBlock) return true;
			generateErrorMessageForBlock(currentInfo.ParentNhNode, node.Value + "マークアップはブロックマークアップ以外のマークアップの中には記述できません。");
			return false;
		}

		private bool checkPreMarkupCanNotBeNested()
		{
			if (!this.parentNhNodeStack.hasBlockMarkup("pre")
				&& !currentInfo.IsMatch(EMagNodeType.BlockMarkup, "pre")) return true;
			using (NhP p = ((NhBlock)currentInfo.ParentNhNode).CreateP())
			{
				this.generateErrorMessage(p, "preマークアップ内に、preマークアップを記述することはできません。");
			}
			return false;
		}

		private readonly bool modulafMode;
		private readonly bool preview;
		public MagXHTMLGenerator(MagNode magTreeRoot, object dummy, bool modulafMode, bool preview)
		{
			this.modulafMode = modulafMode;
			this.preview = preview;
			StringWriter writer = new StringWriter();
			using (NhDiv rootDiv = new NhDiv(writer, null))
			{
				if (modulafMode)
				{
					rootDiv.WriteAttribute("xmlns:it", xmlnsInterim);
				}
				currentInfo = new nestInformation(rootDiv, magTreeRoot);
				foreach (MagNode node in magTreeRoot.Children)
				{
					if (node.Children.Length > 0 && node.Children[0].Type == EMagNodeType.XhtmlFragment)
					{
						generateXhtmlFragment(node.Children[0]);
						continue;
					}
					if (!checkOnlyOneBlockMarkupMustBeInSingleLine(node)) continue;
					if (node.Children.Length > 0 && node.Children[0].Type == EMagNodeType.BlockMarkup)
					{
						if (node.Children[0].Value == "table")
						{
							generateTable(node.Children[0]);
						}
						else
						{
							if (!checkBlockMarkupCanNotBeInTableMarkup(node.Children[0])) continue;
							switch (node.Children[0].Value)
							{
								case "1":
									if (!checkHeadingMarkupMustHaveOnlyText(node.Children[0])) continue;
									if (!checkParentMustBlockMarkup(node.Children[0])) continue;
									generateHx(int.Parse(node.Children[0].Value) + 1, node.Children[0]);
									break;
								case "2":
									goto case "1";
								case "3":
									goto case "1";
								case "4":
									goto case "1";
								case "5":
									goto case "1";
								case "ul":
									generateUl(node.Children[0]);
									break;
								case "ol":
									generateOl(node.Children[0]);
									break;
								case ">":
									generateIndent(node.Children[0]);
									break;
								case "pre":
									generatePre(node.Children[0]);
									break;
								case "pm":
									generateErrorMessageForBlock(currentInfo.ParentNhNode, "pmマークアップはサポートされていません。");
									break;
								case "fl":
									generateErrorMessageForBlock(currentInfo.ParentNhNode, "flマークアップはサポートされていません。");
									break;
								case "cat":
									string argument = getInnerText(node.Children[0]);
									this.Categories.Add(argument);
									break;
								case "xyt":
									generateYouTube(currentInfo.ParentNhNode,
										node.Children[0]);
									break;
								case "ctree":
									generateContentTree(node.Children[0]);
									break;
								case "bq":
									generateBlockQuote(node.Children[0]);
									break;
								case "gm":
									generateLeafletMaps(currentInfo.ParentNhNode, node.Children[0], GoogleMapsType.Normal);
									break;
								case "gmp":
									generateLeafletMaps(currentInfo.ParentNhNode, node.Children[0], GoogleMapsType.Point);
									break;
								case "gml":
									generateLeafletMaps(currentInfo.ParentNhNode, node.Children[0], GoogleMapsType.PolyLine);
									break;
								case "ltx":
									generateLargeText(node.Children[0]);
									break;
								case "fhide":
									generateErrorMessageForBlock(currentInfo.ParentNhNode, "fhideマークアップはサポートされていません。");
									break;
								case "asin":
									generateAsinReference(node.Children[0]);
									break;
								case "asinr":
									generateRepresentAsinReference(node.Children[0]);
									break;
								case "isbn":
									generateAsinReference(node.Children[0]);
									break;
								case "vc":
									generateAsinReference(node.Children[0]);
									break;
							}
						}
					}
					else
					{
						if (currentInfo.TargetMagNode.Type == EMagNodeType.BlockMarkup
							&& currentInfo.TargetMagNode.Value == "table")
						{
							generateTableRaw((NhTable)currentInfo.ParentNhNode, node, tableStartedNow);
							tableStartedNow = false;
						}
						else
						{
							bool oldPreMode = preMode;
							if (currentInfo.TargetMagNode.Type == EMagNodeType.BlockMarkup
								&& currentInfo.TargetMagNode.Value == "pre")
							{
								preMode = true;
							}
							if (currentInfo.ParentNhNode is NhUl)
							{
								using (NhLiInline li = ((NhUl)currentInfo.ParentNhNode).CreateLiInline())
								{
									walkInlineNodes(li, node);
								}
							}
							else if (currentInfo.ParentNhNode is NhOl)
							{
								using (NhLiInline li = ((NhOl)currentInfo.ParentNhNode).CreateLiInline())
								{
									walkInlineNodes(li, node);
								}
							}
							else
							{
								if (node.Children.Length == 0)
								{
									if (modulafMode)
									{
										((NhBlock)currentInfo.ParentNhNode).WriteRawString("<it:whiteSeparatorBetweenParagraphs />\r\n");
									}
									else
									{
										using (NhDiv div = ((NhBlock)currentInfo.ParentNhNode).CreateDiv())
										{
											div.WriteClassAttr("spaceBetweenPara");
										}
									}
								}
								else
								{
									using (NhP p = ((NhBlock)currentInfo.ParentNhNode).CreateP())
									{
										walkInlineNodes(p, node);
									}
								}
							}
							if (currentInfo.TargetMagNode.Type == EMagNodeType.BlockMarkup
								&& currentInfo.TargetMagNode.Value == "pre")
							{
								preMode = oldPreMode;
							}
						}
					}
				}
				parentNhNodeStack.Push(currentInfo);
				while (true)
				{
					currentInfo = parentNhNodeStack.Pop();
					if (object.ReferenceEquals(currentInfo.ParentNhNode, rootDiv)) break;
					if (currentInfo.ParentNhNode is IDisposable)
					{
						((IDisposable)currentInfo.ParentNhNode).Dispose();
					}
				}
				foreach (string errorMessage in errorMessages)
				{
					using (NhP p = rootDiv.CreateP())
					{
						generateErrorMessage(p, errorMessage);
					}
				}
			}
			XhtmlFragmentString = writer.ToString();
		}
		public MagXHTMLGenerator(MagNode magTreeRoot, object attahedFileInfomations)
			: this(magTreeRoot, attahedFileInfomations, false, false)
		{
		}
		public MagXHTMLGenerator(MagNode magTreeRoot)
			: this(magTreeRoot, null)
		{
		}
	}

	public class MagML
	{
		public const string RendererName = "MagML Renderer 0.01 (MagMLParser.DLL)";
		public string RenderedBody;
		public bool EncountedError;
		public ArrayList Categories;
		public List<string> EmbeddedAsins;
		public List<string> RepresentAsins;
		public void Compile(string src, object dummy,
			bool modulafMode, bool preview)
		{
			MagTokenizer tokenizer = new MagTokenizer(src);
			MagParser parser = new MagParser(tokenizer);
			MagXHTMLGenerator generator = new MagXHTMLGenerator(parser.MagTree, dummy, modulafMode, preview);
			RenderedBody = generator.XhtmlFragmentString;
			EncountedError = generator.EncountedError;
			Categories = generator.Categories;
			EmbeddedAsins = generator.EmbeddedAsins;
			RepresentAsins = generator.RepresentAsins;
		}
		public void Compile(string src, object dummy,
			bool modulafMode)
		{
			Compile(src, dummy, modulafMode, false);
		}
	}
}
