using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace Emerge.Checks.Divination
{
    public sealed class LiuYaoPaiPan
    {
        // ==================== 基础数据 ====================

        private static readonly string[] Tiangan = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
        private static readonly string[] Dizhi = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };

        private static readonly Dictionary<string, string> DizhiWuxing = new Dictionary<string, string>
        {
            { "子", "水" }, { "丑", "土" }, { "寅", "木" }, { "卯", "木" },
            { "辰", "土" }, { "巳", "火" }, { "午", "火" }, { "未", "土" },
            { "申", "金" }, { "酉", "金" }, { "戌", "土" }, { "亥", "水" }
        };

        private static readonly Dictionary<string, string> WuxingSheng = new Dictionary<string, string>
        {
            { "木", "火" }, { "火", "土" }, { "土", "金" }, { "金", "水" }, { "水", "木" }
        };

        private static readonly Dictionary<string, string> WuxingKe = new Dictionary<string, string>
        {
            { "木", "土" }, { "土", "水" }, { "水", "火" }, { "火", "金" }, { "金", "木" }
        };

        private static readonly Dictionary<string, string> BagongWuxing = new Dictionary<string, string>
        {
            { "乾", "金" }, { "兑", "金" }, { "离", "火" }, { "震", "木" },
            { "巽", "木" }, { "坎", "水" }, { "艮", "土" }, { "坤", "土" }
        };

        private static readonly Dictionary<string, Dictionary<string, string>> LiuqinRelation =
            new Dictionary<string, Dictionary<string, string>>
            {
                { "金", new Dictionary<string, string> { { "金", "兄弟" }, { "木", "妻财" }, { "水", "子孙" }, { "火", "官鬼" }, { "土", "父母" } } },
                { "木", new Dictionary<string, string> { { "木", "兄弟" }, { "土", "妻财" }, { "火", "子孙" }, { "金", "官鬼" }, { "水", "父母" } } },
                { "水", new Dictionary<string, string> { { "水", "兄弟" }, { "火", "妻财" }, { "木", "子孙" }, { "土", "官鬼" }, { "金", "父母" } } },
                { "火", new Dictionary<string, string> { { "火", "兄弟" }, { "金", "妻财" }, { "土", "子孙" }, { "水", "官鬼" }, { "木", "父母" } } },
                { "土", new Dictionary<string, string> { { "土", "兄弟" }, { "水", "妻财" }, { "金", "子孙" }, { "木", "官鬼" }, { "火", "父母" } } }
            };

        private static readonly Dictionary<string, (string neiGan, string waiGan, string[] neiZhi, string[] waiZhi)> SanGuaNajia =
            new Dictionary<string, (string, string, string[], string[])>
            {
                { "乾", ("甲", "壬", new string[] { "子", "寅", "辰" }, new string[] { "午", "申", "戌" }) },
                { "坤", ("乙", "癸", new string[] { "未", "巳", "卯" }, new string[] { "丑", "亥", "酉" }) },
                { "震", ("庚", "庚", new string[] { "子", "寅", "辰" }, new string[] { "午", "申", "戌" }) },
                { "巽", ("辛", "辛", new string[] { "丑", "亥", "酉" }, new string[] { "未", "巳", "卯" }) },
                { "坎", ("戊", "戊", new string[] { "寅", "辰", "午" }, new string[] { "申", "戌", "子" }) },
                { "离", ("己", "己", new string[] { "卯", "丑", "亥" }, new string[] { "酉", "未", "巳" }) },
                { "艮", ("丙", "丙", new string[] { "辰", "午", "申" }, new string[] { "戌", "子", "寅" }) },
                { "兑", ("丁", "丁", new string[] { "巳", "卯", "丑" }, new string[] { "亥", "酉", "未" }) }
            };

        private static readonly string[] Liushen = { "青龙", "朱雀", "勾陈", "螣蛇", "白虎", "玄武" };
        private static readonly string[] YaoweiNames = { "初爻", "二爻", "三爻", "四爻", "五爻", "上爻" };

        private static readonly Dictionary<string, (string name, string gong, int shiType)> GuaDict =
            new Dictionary<string, (string, string, int)>
            {
                { "111111", ("乾为天", "乾", 0) },
                { "011111", ("天风姤", "乾", 1) },
                { "001111", ("天山遁", "乾", 2) },
                { "000111", ("天地否", "乾", 3) },
                { "000011", ("风地观", "乾", 4) },
                { "000001", ("山地剥", "乾", 5) },
                { "000101", ("火地晋", "乾", 6) },
                { "111101", ("火天大有", "乾", 7) },

                { "110110", ("兑为泽", "兑", 0) },
                { "010110", ("泽水困", "兑", 1) },
                { "000110", ("泽地萃", "兑", 2) },
                { "001110", ("泽山咸", "兑", 3) },
                { "001010", ("水山蹇", "兑", 4) },
                { "001000", ("地山谦", "兑", 5) },
                { "001100", ("雷山小过", "兑", 6) },
                { "110100", ("雷泽归妹", "兑", 7) },

                { "101101", ("离为火", "离", 0) },
                { "001101", ("火山旅", "离", 1) },
                { "011101", ("火风鼎", "离", 2) },
                { "010101", ("火水未济", "离", 3) },
                { "010001", ("山水蒙", "离", 4) },
                { "010011", ("风水涣", "离", 5) },
                { "010111", ("天水讼", "离", 6) },
                { "101111", ("天火同人", "离", 7) },

                { "100100", ("震为雷", "震", 0) },
                { "000100", ("雷地豫", "震", 1) },
                { "010100", ("雷水解", "震", 2) },
                { "011100", ("雷风恒", "震", 3) },
                { "011000", ("地风升", "震", 4) },
                { "011010", ("水风井", "震", 5) },
                { "011110", ("泽风大过", "震", 6) },
                { "100110", ("泽雷随", "震", 7) },

                { "011011", ("巽为风", "巽", 0) },
                { "111011", ("风天小畜", "巽", 1) },
                { "101011", ("风火家人", "巽", 2) },
                { "100011", ("风雷益", "巽", 3) },
                { "100111", ("天雷无妄", "巽", 4) },
                { "100101", ("火雷噬嗑", "巽", 5) },
                { "100001", ("山雷颐", "巽", 6) },
                { "011001", ("山风蛊", "巽", 7) },

                { "010010", ("坎为水", "坎", 0) },
                { "110010", ("水泽节", "坎", 1) },
                { "100010", ("水雷屯", "坎", 2) },
                { "101010", ("水火既济", "坎", 3) },
                { "101110", ("泽火革", "坎", 4) },
                { "101100", ("雷火丰", "坎", 5) },
                { "101000", ("地火明夷", "坎", 6) },
                { "010000", ("地水师", "坎", 7) },

                { "001001", ("艮为山", "艮", 0) },
                { "101001", ("山火贲", "艮", 1) },
                { "111001", ("山天大畜", "艮", 2) },
                { "110001", ("山泽损", "艮", 3) },
                { "110101", ("火泽睽", "艮", 4) },
                { "110111", ("天泽履", "艮", 5) },
                { "110011", ("风泽中孚", "艮", 6) },
                { "001011", ("风山渐", "艮", 7) },

                { "000000", ("坤为地", "坤", 0) },
                { "100000", ("地雷复", "坤", 1) },
                { "110000", ("地泽临", "坤", 2) },
                { "111000", ("地天泰", "坤", 3) },
                { "111100", ("雷天大壮", "坤", 4) },
                { "111110", ("泽天夬", "坤", 5) },
                { "111010", ("水天需", "坤", 6) },
                { "000010", ("水地比", "坤", 7) }
            };

        // ==================== 结果数据结构（可调用） ====================

        [Serializable]
        public sealed class YaoResult
        {
            public int index;
            public string yaowei;
            public string liushen;
            public int yao6789;
            public string yinyangName;
            public bool benIsYang;
            public string benSymbol;
            public string benGanzhi;
            public string benWuxing;
            public string benLiuqin;
            public bool isShi;
            public bool isYing;
            public bool isDongYao;
            public bool bianIsYang;
            public string bianSymbol;
            public string bianGanzhi;
            public string bianWuxing;
            public string bianLiuqin;
            public int wangshuaiScore;
            public List<string> wangshuaiDetails;
        }

        [Serializable]
        public sealed class PaiPanResult
        {
            public string yueling;
            public string richen;
            public int[] yao6789;
            public int[] bengua;
            public int[] biangua;

            public string benGuaName;
            public string benGong;
            public string benGongWuxing;
            public int benShiType;

            public string bianGuaName;
            public string bianGong;

            public int shiYaoIndex;
            public int yingYaoIndex;

            public List<YaoResult> yaos;

            public LiuqinScore[] liuqinScores;
            public int[] behaviorModifiers;

            // Save arrays; expose a fresh read-only dictionary to callers.
            public IReadOnlyDictionary<string, int> liuqinSummary
            {
                get
                {
                    var summary = new Dictionary<string, int>(StringComparer.Ordinal);
                    if (liuqinScores != null)
                        foreach (var entry in liuqinScores)
                            if (entry != null && !string.IsNullOrWhiteSpace(entry.liuqin))
                                summary[entry.liuqin] = entry.score;
                    return new ReadOnlyDictionary<string, int>(summary);
                }
            }
            public IReadOnlyDictionary<string, int> liuQinSummary => liuqinSummary;
        }

        [Serializable]
        public sealed class LiuqinScore
        {
            public string liuqin;
            public int score;
        }

        // ==================== 工具方法 ====================

        public static bool TryNormalizeCalendar(string month, string day,
            out string normalizedMonth, out string normalizedDay, out string error)
        {
            normalizedMonth = null;
            normalizedDay = null;
            error = null;
            try
            {
                string branch = ParseMonth(month);
                ParseDay(day, out string stem, out string dayBranch);
                normalizedMonth = branch + "月";
                normalizedDay = stem + dayBranch + "日";
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ParseMonth(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("月令不能为空。", nameof(value));
            string text = value.Trim();
            if (text.EndsWith("月", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 1);
            if (text.Length != 1 || Array.IndexOf(Dizhi, text) < 0)
                throw new ArgumentException("月令应为地支或地支加月，例如巳月。", nameof(value));
            return text;
        }

        private static void ParseDay(string value, out string gan, out string zhi)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("日辰不能为空。", nameof(value));
            string text = value.Trim();
            if (text.EndsWith("日", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 1);
            if (text.Length != 2 || Array.IndexOf(Tiangan, text.Substring(0, 1)) < 0 ||
                Array.IndexOf(Dizhi, text.Substring(1, 1)) < 0)
                throw new ArgumentException("日辰应为天干加地支，可带日，例如戊子日。", nameof(value));
            gan = text.Substring(0, 1);
            zhi = text.Substring(1, 1);
        }

        private static string GuaToKey(int[] gua)
        {
            string result = "";
            for (int i = 0; i < gua.Length; i++) result += gua[i].ToString();
            return result;
        }

        private static string GetSanGuaName(int yao1, int yao2, int yao3)
        {
            string key = "" + yao1 + yao2 + yao3;
            switch (key)
            {
                case "111": return "乾";
                case "000": return "坤";
                case "100": return "震";
                case "011": return "巽";
                case "010": return "坎";
                case "101": return "离";
                case "001": return "艮";
                case "110": return "兑";
                default: throw new ArgumentException("三爻编码只能包含 0 和 1。");
            }
        }

        private static (string[] tiangan, string[] dizhi) GetNajia(int[] gua)
        {
            string neiGuaName = GetSanGuaName(gua[0], gua[1], gua[2]);
            string waiGuaName = GetSanGuaName(gua[3], gua[4], gua[5]);

            var neiNajia = SanGuaNajia[neiGuaName];
            var waiNajia = SanGuaNajia[waiGuaName];

            string[] tiangan = new string[6];
            string[] dizhi = new string[6];

            tiangan[0] = neiNajia.neiGan; dizhi[0] = neiNajia.neiZhi[0];
            tiangan[1] = neiNajia.neiGan; dizhi[1] = neiNajia.neiZhi[1];
            tiangan[2] = neiNajia.neiGan; dizhi[2] = neiNajia.neiZhi[2];
            tiangan[3] = waiNajia.waiGan; dizhi[3] = waiNajia.waiZhi[0];
            tiangan[4] = waiNajia.waiGan; dizhi[4] = waiNajia.waiZhi[1];
            tiangan[5] = waiNajia.waiGan; dizhi[5] = waiNajia.waiZhi[2];

            return (tiangan, dizhi);
        }

        private static string GetLiuqin(string gongWuxing, string yaoWuxing)
        {
            if (LiuqinRelation.ContainsKey(gongWuxing))
            {
                var dict = LiuqinRelation[gongWuxing];
                if (dict.ContainsKey(yaoWuxing))
                    return dict[yaoWuxing];
            }
            return "未知";
        }

        private static string[] CalculateLiushen(string richenTiangan)
        {
            int startIndex = 0;
            switch (richenTiangan)
            {
                case "甲": case "乙": startIndex = 0; break;
                case "丙": case "丁": startIndex = 1; break;
                case "戊": startIndex = 2; break;
                case "己": startIndex = 3; break;
                case "庚": case "辛": startIndex = 4; break;
                case "壬": case "癸": startIndex = 5; break;
            }

            string[] result = new string[6];
            for (int i = 0; i < 6; i++) result[i] = Liushen[(startIndex + i) % 6];
            return result;
        }

        private static int GetShengKeScore(string sourceWuxing, string targetWuxing)
        {
            if (WuxingSheng.ContainsKey(sourceWuxing) && WuxingSheng[sourceWuxing] == targetWuxing) return 1;
            if (WuxingKe.ContainsKey(sourceWuxing) && WuxingKe[sourceWuxing] == targetWuxing) return -1;
            return 0;
        }

        private static int GetShiYaoIndex(int shiType)
        {
            switch (shiType)
            {
                case 0: return 5;
                case 1: return 0;
                case 2: return 1;
                case 3: return 2;
                case 4: return 3;
                case 5: return 4;
                case 6: return 3;
                case 7: return 2;
                default: return 5;
            }
        }

        private static void ConvertYao6789(int[] yao6789, out int[] bengua, out int[] biangua)
        {
            bengua = new int[6];
            biangua = new int[6];

            for (int i = 0; i < 6; i++)
            {
                switch (yao6789[i])
                {
                    case 6: bengua[i] = 0; biangua[i] = 1; break; // 老阴动
                    case 7: bengua[i] = 1; biangua[i] = 1; break; // 少阳静
                    case 8: bengua[i] = 0; biangua[i] = 0; break; // 少阴静
                    case 9: bengua[i] = 1; biangua[i] = 0; break; // 老阳动
                    default: throw new ArgumentException("爻值必须为 6、7、8、9。", nameof(yao6789));
                }
            }
        }

        private static string GetYao6789Name(int n)
        {
            switch (n)
            {
                case 6: return "老阴";
                case 7: return "少阳";
                case 8: return "少阴";
                case 9: return "老阳";
                default: return "未知";
            }
        }

        // ==================== 核心排盘方法 ====================

        /// <summary>
        /// 六爻排盘主方法
        /// </summary>
        /// <param name="yueling">月令，格式：地支+月，如 "寅月"、"子月"</param>
        /// <param name="richen">日辰，格式：天干+地支+日，如 "甲子日"、"丙申日"</param>
        /// <param name="yao6789">摇卦结果，6个整数，从初爻到上爻，每个值为 6/7/8/9：
        /// 6=老阴(动)、7=少阳(静)、8=少阴(静)、9=老阳(动)</param>
        /// <returns>完整排盘结果</returns>
        public PaiPanResult PaiPan(string yueling, string richen, int[] yao6789)
        {
            if (yao6789 == null || yao6789.Length != 6)
                throw new ArgumentException("爻值必须为初爻到上爻的六个整数。", nameof(yao6789));
            for (int i = 0; i < 6; i++)
                if (yao6789[i] < 6 || yao6789[i] > 9)
                    throw new ArgumentException("每个爻值只能为 6、7、8、9。", nameof(yao6789));
            string yuelingZhi = ParseMonth(yueling);
            ParseDay(richen, out string richenGan, out string richenZhi);
            yueling = yuelingZhi + "月";
            richen = richenGan + richenZhi + "日";

            PaiPanResult result = new PaiPanResult();

            int[] bengua, biangua;
            ConvertYao6789(yao6789, out bengua, out biangua);

            result.yueling = yueling;
            result.richen = richen;
            result.yao6789 = (int[])yao6789.Clone();
            result.bengua = bengua;
            result.biangua = biangua;

            string benKey = GuaToKey(bengua);
            string bianKey = GuaToKey(biangua);

            if (!GuaDict.ContainsKey(benKey) || !GuaDict.ContainsKey(bianKey))
                throw new ArgumentException("卦编码无效。", nameof(yao6789));

            var benInfo = GuaDict[benKey];
            var bianInfo = GuaDict[bianKey];

            result.benGuaName = benInfo.name;
            result.benGong = benInfo.gong;
            result.benGongWuxing = BagongWuxing[benInfo.gong];
            result.benShiType = benInfo.shiType;
            result.shiYaoIndex = GetShiYaoIndex(benInfo.shiType);
            result.yingYaoIndex = (result.shiYaoIndex + 3) % 6;

            result.bianGuaName = bianInfo.name;
            result.bianGong = bianInfo.gong;

            var benNajia = GetNajia(bengua);
            var bianNajia = GetNajia(biangua);

            string[] liushenList = CalculateLiushen(richenGan);
            string yuelingWuxing = DizhiWuxing[yuelingZhi];
            string richenWuxing = DizhiWuxing[richenZhi];

            List<int> dongYaoList = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                if (bengua[i] != biangua[i]) dongYaoList.Add(i);
            }

            result.yaos = new List<YaoResult>();
            for (int i = 0; i < 6; i++)
            {
                YaoResult yao = new YaoResult();
                yao.index = i;
                yao.yaowei = YaoweiNames[i];
                yao.liushen = liushenList[i];
                yao.yao6789 = yao6789[i];
                yao.yinyangName = GetYao6789Name(yao6789[i]);

                yao.benIsYang = bengua[i] == 1;
                yao.benSymbol = bengua[i] == 1 ? "━━━━━" : "━━ ━━";
                yao.benGanzhi = benNajia.tiangan[i] + benNajia.dizhi[i];
                yao.benWuxing = DizhiWuxing[benNajia.dizhi[i]];
                yao.benLiuqin = GetLiuqin(result.benGongWuxing, yao.benWuxing);

                yao.isShi = (i == result.shiYaoIndex);
                yao.isYing = (i == result.yingYaoIndex);

                yao.isDongYao = (bengua[i] != biangua[i]);

                yao.bianIsYang = biangua[i] == 1;
                yao.bianSymbol = biangua[i] == 1 ? "━━━━━" : "━━ ━━";
                yao.bianGanzhi = bianNajia.tiangan[i] + bianNajia.dizhi[i];
                yao.bianWuxing = DizhiWuxing[bianNajia.dizhi[i]];
                yao.bianLiuqin = GetLiuqin(result.benGongWuxing, yao.bianWuxing);

                yao.wangshuaiScore = 0;
                yao.wangshuaiDetails = new List<string>();

                int yueScore = GetShengKeScore(yuelingWuxing, yao.benWuxing);
                if (yueScore == 1) { yao.wangshuaiScore += 1; yao.wangshuaiDetails.Add("月令生 +1"); }
                else if (yueScore == -1) { yao.wangshuaiScore -= 1; yao.wangshuaiDetails.Add("月令克 -1"); }
                else yao.wangshuaiDetails.Add("月令无作用 0");

                int riScore = GetShengKeScore(richenWuxing, yao.benWuxing);
                if (riScore == 1) { yao.wangshuaiScore += 1; yao.wangshuaiDetails.Add("日辰生 +1"); }
                else if (riScore == -1) { yao.wangshuaiScore -= 1; yao.wangshuaiDetails.Add("日辰克 -1"); }
                else yao.wangshuaiDetails.Add("日辰无作用 0");

                foreach (int dongIdx in dongYaoList)
                {
                    if (dongIdx == i) continue;
                    string dongWuxing = DizhiWuxing[benNajia.dizhi[dongIdx]];
                    int dongScore = GetShengKeScore(dongWuxing, yao.benWuxing);
                    if (dongScore == 1) { yao.wangshuaiScore += 1; yao.wangshuaiDetails.Add(YaoweiNames[dongIdx] + "(动)生 +1"); }
                    else if (dongScore == -1) { yao.wangshuaiScore -= 1; yao.wangshuaiDetails.Add(YaoweiNames[dongIdx] + "(动)克 -1"); }
                }

                if (yao.isDongYao)
                {
                    string bianWuxing = DizhiWuxing[bianNajia.dizhi[i]];
                    int bianScore = GetShengKeScore(bianWuxing, yao.benWuxing);
                    if (bianScore == 1) { yao.wangshuaiScore += 1; yao.wangshuaiDetails.Add("变爻生 +1"); }
                    else if (bianScore == -1) { yao.wangshuaiScore -= 1; yao.wangshuaiDetails.Add("变爻克 -1"); }
                    else yao.wangshuaiDetails.Add("变爻无作用 0");
                }

                result.yaos.Add(yao);
            }

            var summary = SummarizeLiuqin(result.yaos);
            result.liuqinScores = new[] { "兄弟", "妻财", "子孙", "官鬼", "父母" }
                .Select(name => new LiuqinScore { liuqin = name, score = summary[name] }).ToArray();
            result.behaviorModifiers = new[]
            {
                summary["父母"], summary["子孙"], summary["官鬼"], summary["妻财"], summary["兄弟"],
                result.yaos[result.shiYaoIndex].wangshuaiScore
            };

            return result;
        }

        private Dictionary<string, int> SummarizeLiuqin(List<YaoResult> yaos)
        {
            Dictionary<string, int> summary = new Dictionary<string, int>
            {
                { "兄弟", 0 }, { "妻财", 0 }, { "子孙", 0 }, { "官鬼", 0 }, { "父母", 0 }
            };

            Dictionary<string, List<int>> scores = new Dictionary<string, List<int>>();
            foreach (var k in summary.Keys.ToList()) scores[k] = new List<int>();

            foreach (var yao in yaos)
            {
                if (scores.ContainsKey(yao.benLiuqin))
                {
                    scores[yao.benLiuqin].Add(yao.wangshuaiScore);
                }
            }

            foreach (var kv in scores)
            {
                if (kv.Value.Count > 0)
                {
                    int max = int.MinValue;
                    foreach (int v in kv.Value) if (v > max) max = v;
                    summary[kv.Key] = max;
                }
                else
                {
                    summary[kv.Key] = 0;
                }
            }

            return summary;
        }

        public static bool IsValidResult(PaiPanResult result)
        {
            if (result == null) return false;
            PaiPanResult expected;
            try { expected = new LiuYaoPaiPan().PaiPan(result.yueling, result.richen, result.yao6789); }
            catch (ArgumentException) { return false; }
            if (result.yueling != expected.yueling || result.richen != expected.richen ||
                result.benGuaName != expected.benGuaName || result.benGong != expected.benGong ||
                result.benGongWuxing != expected.benGongWuxing || result.benShiType != expected.benShiType ||
                result.bianGuaName != expected.bianGuaName || result.bianGong != expected.bianGong ||
                result.shiYaoIndex != expected.shiYaoIndex || result.yingYaoIndex != expected.yingYaoIndex ||
                !EqualInts(result.bengua, expected.bengua) || !EqualInts(result.biangua, expected.biangua) ||
                !EqualInts(result.behaviorModifiers, expected.behaviorModifiers) || result.yaos == null ||
                result.yaos.Count != 6 || result.liuqinScores == null || result.liuqinScores.Length != 5) return false;
            for (int i = 0; i < 6; i++)
            {
                var actual = result.yaos[i];
                var valid = expected.yaos[i];
                if (actual == null || actual.index != valid.index || actual.yaowei != valid.yaowei ||
                    actual.liushen != valid.liushen || actual.yao6789 != valid.yao6789 || actual.yinyangName != valid.yinyangName ||
                    actual.benIsYang != valid.benIsYang || actual.benSymbol != valid.benSymbol ||
                    actual.benGanzhi != valid.benGanzhi || actual.benWuxing != valid.benWuxing || actual.benLiuqin != valid.benLiuqin ||
                    actual.isShi != valid.isShi || actual.isYing != valid.isYing || actual.isDongYao != valid.isDongYao ||
                    actual.bianIsYang != valid.bianIsYang || actual.bianSymbol != valid.bianSymbol ||
                    actual.bianGanzhi != valid.bianGanzhi || actual.bianWuxing != valid.bianWuxing || actual.bianLiuqin != valid.bianLiuqin ||
                    actual.wangshuaiScore != valid.wangshuaiScore || actual.wangshuaiDetails == null ||
                    !actual.wangshuaiDetails.SequenceEqual(valid.wangshuaiDetails)) return false;
            }
            for (int i = 0; i < 5; i++)
                if (result.liuqinScores[i] == null || result.liuqinScores[i].liuqin != expected.liuqinScores[i].liuqin ||
                    result.liuqinScores[i].score != expected.liuqinScores[i].score) return false;
            return true;
        }

        private static bool EqualInts(int[] first, int[] second)
        {
            return first != null && second != null && first.SequenceEqual(second);
        }

        // ==================== 格式化输出 ====================

        public string PaiPanToString(PaiPanResult r)
        {
            if (r == null) return "排盘失败";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.AppendLine("==================== 六爻排盘 ====================");
            sb.AppendLine();
            sb.AppendLine("【输入参数】");
            sb.AppendLine("月令: " + r.yueling);
            sb.AppendLine("日辰: " + r.richen);

            sb.Append("摇卦: ");
            for (int i = 0; i < 6; i++) sb.Append(r.yao6789[i] + " ");
            sb.AppendLine(" (初爻→上爻)");

            sb.AppendLine();
            sb.AppendLine("【排盘结果】");
            sb.AppendLine("本卦: " + r.benGuaName + " (" + r.benGong + "宫, " + r.benGongWuxing + ")");
            sb.AppendLine("变卦: " + r.bianGuaName);
            sb.AppendLine();

            sb.AppendLine("【本卦 " + r.benGuaName + "】");
            sb.AppendLine("爻位\t六神\t摇得\t本卦\t\t干支\t五行\t六亲\t世应");
            sb.AppendLine("------------------------------------------------------------------------");
            for (int i = 5; i >= 0; i--)
            {
                var y = r.yaos[i];
                string shiying = "";
                if (y.isShi) shiying = "世";
                if (y.isYing) shiying = "应";
                string dong = y.isDongYao ? " *" : "";
                sb.AppendLine(y.yaowei + "\t" + y.liushen + "\t" + y.yao6789 + y.yinyangName + "\t" + y.benSymbol + "\t" + y.benGanzhi + "\t" + y.benWuxing + "\t" + y.benLiuqin + "\t" + shiying + dong);
            }

            sb.AppendLine();
            sb.AppendLine("【变卦 " + r.bianGuaName + "】");
            sb.AppendLine("爻位\t变卦\t\t干支\t五行\t六亲\t动爻");
            sb.AppendLine("------------------------------------------------------------------------");
            for (int i = 5; i >= 0; i--)
            {
                var y = r.yaos[i];
                string dong = y.isDongYao ? "★动" : "";
                sb.AppendLine(y.yaowei + "\t" + y.bianSymbol + "\t" + y.bianGanzhi + "\t" + y.bianWuxing + "\t" + y.bianLiuqin + "\t" + dong);
            }

            sb.AppendLine();
            sb.AppendLine("【详细信息】");
            sb.AppendLine("本卦宫位: " + r.benGong + "宫");
            sb.AppendLine("本宫五行: " + r.benGongWuxing);
            sb.AppendLine("世爻位置: " + YaoweiNames[r.shiYaoIndex]);
            sb.AppendLine("应爻位置: " + YaoweiNames[r.yingYaoIndex]);

            List<string> dongList = new List<string>();
            for (int i = 0; i < 6; i++) if (r.yaos[i].isDongYao) dongList.Add(r.yaos[i].yaowei);
            if (dongList.Count > 0)
                sb.AppendLine("动爻: " + string.Join(", ", dongList.ToArray()));
            else
                sb.AppendLine("动爻: 无（静卦）");

            sb.AppendLine();
            sb.AppendLine("【旺衰分析】");
            sb.AppendLine("爻位\t六亲\t五行\t得分\t明细");
            sb.AppendLine("------------------------------------------------------------------------");
            for (int i = 5; i >= 0; i--)
            {
                var y = r.yaos[i];
                string details = string.Join(", ", y.wangshuaiDetails.ToArray());
                string dong = y.isDongYao ? " [动]" : "";
                sb.AppendLine(y.yaowei + "\t" + y.benLiuqin + "\t" + y.benWuxing + "\t" + y.wangshuaiScore + "\t" + details + dong);
            }

            sb.AppendLine();
            sb.AppendLine("六亲最终得分（缺六亲计0，多爻取最高）：");
            sb.AppendLine("六亲\t得分");
            sb.AppendLine("----------------");
            foreach (var kv in r.liuqinSummary)
            {
                sb.AppendLine(kv.Key + "\t" + kv.Value);
            }

            sb.AppendLine("我 / 认知（取世爻得分）: " + r.behaviorModifiers[5]);
            sb.AppendLine("==================================================");

            return sb.ToString();
        }

    }
}
