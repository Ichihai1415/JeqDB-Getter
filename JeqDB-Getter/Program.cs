using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace JeqDB_Getter
{
    internal class Program
    {
        public static HttpClient client = new();

        static void Main(string[] args)
        {
            var addText = File.Exists("lastGet.dat") ? "最終取得は " + File.ReadAllText("lastGet.dat") + " です。" : "1919/01/01から有効です。";
            var startDate = ConAsk<DateTime>("開始日を入力してください。" + addText);
            var EndDate = ConAsk<DateTime>("終了日を入力してください。" + (DateTime.Now.Hour < 3 ? ("三日前の " + DateTime.Now.AddDays(-3).ToString("yyyy/MM/dd")) : ("二日前の " + DateTime.Now.AddDays(-2).ToString("yyyy/MM/dd"))) + " がおすすめです。");
            DateTime getDate;
            ConWrite($"高速大量アクセスを防ぐため取得毎に1秒待機します。予想処理時間は{(EndDate - startDate).TotalDays}秒({(EndDate - startDate).TotalDays / 60d:F1}分)+取得・処理時間です。");
            for (getDate = startDate; getDate <= EndDate; getDate += TimeSpan.FromDays(1))
                GetDB1d(getDate);
            ConWrite("終了しました。");
        }

        /// <summary>
        /// 震度データベース1日分取得
        /// </summary>
        /// <param name="getDate">取得日</param>
        public static void GetDB1d(DateTime getDate)
        {
            try
            {
                var savePath = $"output\\{getDate.Year}\\{getDate.Month}\\{getDate.Day}.csv";
                var response = Regex.Unescape(client.GetStringAsync($"https://www.data.jma.go.jp/svd/eqdb/data/shindo/api/api.php?mode=search&dateTimeF[]={getDate:yyyy-MM-dd}&dateTimeF[]=00:00&dateTimeT[]={getDate:yyyy-MM-dd}&dateTimeT[]=23:59&mag[]=0.0&mag[]=9.9&dep[]=0&dep[]=999&epi[]=99&pref[]=99&city[]=99&station[]=99&obsInt=1&maxInt=1&additionalC=true&Sort=S0&Comp=C0&seisCount=false&observed=false").Result);
                var json = JsonNode.Parse(response);

                var csv = new StringBuilder("地震の発生日,地震の発生時刻,震央地名,緯度,経度,深さ,Ｍ,最大震度\n");
                var res = json!["res"];
                string viewText;
                if (res is JsonArray jsonArray)
                {
                    foreach (var data in res.AsArray())
                    {
                        var ot = (string?)data!["ot"]!.AsValue();
                        if (ot == null)
                            continue;
                        csv.Append(ot.Replace(" ", ","));
                        csv.Append(',');
                        csv.Append((string?)data["name"]!.AsValue());
                        csv.Append(',');
                        csv.Append((string?)data["latS"]!.AsValue());
                        csv.Append(',');
                        csv.Append((string?)data["lonS"]!.AsValue());
                        csv.Append(',');
                        csv.Append((string?)data["dep"]!.AsValue());
                        csv.Append(',');
                        csv.Append((string?)data["mag"]!.AsValue());
                        csv.Append(',');
                        csv.Append((string?)data["maxI"]!.AsValue());
                        csv.AppendLine();
                    }
                    viewText = "検索結果地震数 ： " + res.AsArray().Count;
                }
                else
                    viewText = (string)res!.AsValue()!;

                Directory.CreateDirectory("output");
                Directory.CreateDirectory($"output\\{getDate.Year}");
                Directory.CreateDirectory($"output\\{getDate.Year}\\{getDate.Month}");
                File.WriteAllText(savePath, csv.ToString());
                File.WriteAllText("lastGet.dat", getDate.ToString("yyyy/MM/dd"));
                ConWrite(getDate.ToString("yyyy/MM/dd") + " -> " + viewText);
                if (getDate.Day == 1)
                    GC.Collect();
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                ConWrite(ex);
            }
        }
        /// <summary>
        /// ユーザーに値の入力を求めます。
        /// </summary>
        /// <remarks>入力値が変換可能なとき返ります。</remarks>
        /// <typeparam name="T">変換するタイプ</typeparam>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="resType">変換するタイプ</param>
        /// <param name="nullText">何も入力されなかった場合に選択</param>
        /// <returns><paramref name="resType"/>で指定したタイプに変換された入力された値</returns>
        public static T? ConAsk<T>(string message, string? nullText = null)//もうちょっといい感じに書きたい
        {
            while (true)
                try
                {
                    ConWrite(message);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    var input = Console.ReadLine();
                    if (string.IsNullOrEmpty(input))
                    {
                        if (string.IsNullOrEmpty(nullText))
                            throw new Exception("値を入力してください。");
                        input = nullText;
                        ConWrite(nullText + "(自動入力)", ConsoleColor.Cyan);
                    }
                    return Type.GetTypeCode(typeof(T)) switch//timespan:typecode.objectになるから別で
                    {
                        TypeCode.String => (T)(object)input,//ゴリ押し実装
                        TypeCode.Int32 => (T)(object)int.Parse(input),
                        TypeCode.Double => (T)(object)double.Parse(input),
                        TypeCode.DateTime => (T)(object)DateTime.Parse(input),
                        _ => ConAskSubConverter<T>(input)
                    };
                }
                catch (Exception ex)
                {
                    ConWrite("入力の処理に失敗しました。" + ex.Message, ConsoleColor.Red);
                }
        }

        private static T? ConAskSubConverter<T>(string input)
        {
            if (typeof(T) == typeof(TimeSpan))
                return (T)(object)TimeSpan.Parse(input);
            try
            {
                return (T)Convert.ChangeType(input, typeof(T));
            }
            catch (Exception ex)
            {
                Console.WriteLine("変換に失敗しました。" + ex.Message);
                return default;
            }
        }

        /// <summary>
        /// コンソールのデフォルトの色
        /// </summary>
        public static readonly ConsoleColor defaultColor = Console.ForegroundColor;

        /// <summary>
        /// コンソールにデフォルトの色で出力します。
        /// </summary>
        /// <param name="text">出力するテキスト</param>
        /// <param name="withLine">改行するか</param>
        public static void ConWrite(string? text, bool withLine = true)
        {
            ConWrite(text, defaultColor, withLine);
        }

        /// <summary>
        /// 例外のテキストを赤色で出力します。
        /// </summary>
        /// <param name="ex">出力する例外</param>
        public static void ConWrite(Exception ex)
        {
            ConWrite(ex.ToString(), ConsoleColor.Red);
        }

        /// <summary>
        /// コンソールに色付きで出力します。色は変わったままとなります。
        /// </summary>
        /// <param name="text">出力するテキスト</param>
        /// <param name="color">表示する色</param>
        /// <param name="withLine">改行するか</param>
        public static void ConWrite(string? text, ConsoleColor color, bool withLine = true)
        {
            Console.ForegroundColor = color;
            if (withLine)
                Console.WriteLine(text);
            else
                Console.Write(text);
        }
    }
}
