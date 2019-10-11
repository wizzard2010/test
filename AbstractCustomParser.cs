using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Roadkill.Core.Enums;
using Roadkill.Core.Models;
using Roadkill.Core.Text.Parsers.Custom;

namespace Roadkill.Core.Text.Parsers
{
    using System.Windows.Documents;

    using Roadkill.Core.Configuration;
    using Roadkill.Core.Database;
    using Roadkill.Core.DomainObjects;
    using Roadkill.Core.Text.Parsers.Styler;

    using StructureMap;

    public abstract class AbstractCustomParser
    {
        public static string StartInfoTableToken = "{{lan|infotable|";
        public static string EndInfoTableToken = "}}";

        public static string StartTableToken = "{|";
        public static string EndTableToken = "|}";
        public static string HeaderTableToken = "|+";
        public static string RowTableToken = "|-";

        public static string HeadTokenStr
        {
            get { return new string(HeadToken, 1); }
        }

        public static string CellTokenStr
        {
            get { return new string(CellToken, 1); }
        }

        public static string OrderedListBegin = "<ol";
        public static string OrderedListEnd = "</ol>";
        public static string UnorderedListBegin = "<ul";
        public static string UnorderedListEnd = "</ul>";

        public const string VideoStartToken = "{{lan|video|";
        public const string AudioStartToken = "{{lan|audio|";
        public const string LanDiskStartToken = "{{lan|disk|file";

        private const string StartHeaderToken = "header=";
        private const string EndHeaderToken = "|";
        private const char CellToken = '|';
        private const char HeadToken = '^';
        private const string CellAlignToken = "  ";

        private readonly IAlignParser _alignParser = new AlignParser();
        private readonly ICellSpanParser _cellSpanParser = new CellSpanParser();

        protected static readonly Regex ImageRegex = new Regex(@"{{\s{0,}wiki\:(?'src'.*?)(\?.*?)*(\|(?'alt'(\n|.)*?))*\s*}}");
        protected static readonly Regex VideoRegex = new Regex(@"\{\{\s{0,}lan\|video\|\/{0,1}(?'src'(\n|.)*?)(?'size'\?.*?)?(\|(?'fs'(\s*filesystem\s*=\s*true\s*)*?))?(\|(?'title'(\n|.)*?))?s*\}\}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        protected static readonly Regex AudioRegex = new Regex(@"\{\{\s{0,}lan\|audio\|\/{0,1}(?'src'(\n|.)*?)(\|(?'fs'(\s*filesystem\s*=\s*true\s*)*?))?(\|(?'title'(\n|.)*?))?s*\}\}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        protected static readonly Regex FileRegex = new Regex(@"\{\{lan\|files\| *file\s*=\s*\/{0,1}(?'src'(\n|.)*?)(\|((\n|.)*?))?\}\}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        protected static readonly Regex ColspanRegex = new Regex(@"colspan\s*=\s*""(\d+)""");
        protected static readonly Regex RowspanRegex = new Regex(@"rowspan\s*=\s*""(\d+)""");
        protected static readonly Regex ExcelRegex = new Regex(@"\{\{\s*#invoke:Chart(.*?)fromExcel\s*=s*(?'src'.*?)!", RegexOptions.Singleline);

        protected static readonly Regex LanDiskFileRegex = new Regex(@"\{\{\s*lan\|disk\|file\s*=\s*(?<file>.[^\|]+)\|+(name|title){1}\s*=\s*(?<title>.*[^\|]*)\|+viewtype\s*=\s*(?<viewtype>.[^\|]+)\|+place\s*=\s*(?<place>.[^\|]+)\|\s*\}\}", RegexOptions.IgnoreCase);
        protected static readonly Regex LanDiskFolderRegex = new Regex(@"\{\{\s*lan\|disk\|folder\s*=\s*(?<folder>.[^\|]+)\|+(name|title){1}\s*=\s*(?<title>.*[^\|]*)\|+viewtype\s*=\s*(?<viewtype>.[^\|]+)\|+place\s*=\s*(?<place>.[^\|]+)\|\s*\}\}", RegexOptions.IgnoreCase);
        protected static readonly Regex LanDiskFilterRegex = new Regex(@"\{\{\s*lan\|disk\|filter\s*=\s*(?<filter>.[^\|]+)\|+(name|title){1}\s*=\s*(?<title>.*[^\|]*)\|+viewtype\s*=\s*(?<viewtype>.[^\|]+)\|+place\s*=\s*(?<place>.[^\|]+)\|\s*\}\}", RegexOptions.IgnoreCase);

        protected const string LanDiskFileRegexBeforeFile = @"\{\{\s*lan\|disk\|file\s*=\s*";
        protected const string LanDiskFileRegexAfterFile = @"\|+(name|title){1}\s*=\s*(?<title>.*[^\|]*)\|+viewtype\s*=\s*(?<viewtype>.[^\|]+)\|+place\s*=\s*(?<place>.[^\|]+)\|\s*\}\}";
        protected const string LanDiskFolderRegexBeforeFolder = @"\{\{\s*lan\|disk\|folder\s*=\s*";
        protected const string LanDiskFolderRegexAfterFolder = @"/*\|+(name|title){1}\s*=\s*(?<title>.*[^\|]*)\|+viewtype\s*=\s*(?<viewtype>.[^\|]+)\|+place\s*=\s*(?<place>.[^\|]+)\|\s*\}\}";

        // старый синтаксис
        protected static readonly Regex OldFileToArticleRegex = new Regex(@"\{\{\s{0,}(?'oldType'wiki\:){1}(?'file'.*?)(\?.*?)*(\|(?'title'(\n|.)*?))*\s*\}\}", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        protected static readonly Regex OldAudioVideoToArticleRegex = new Regex(@"\{\{\s{0,}(?'oldType'lan\|audio|lan\|video)\|\/{0,1}(?'file'(\n|.)*?)(\|(?'fs'(\s*filesystem\s*=\s*true\s*)*?))?(\|(?'title'(\n|.)*?))?\s*\}\}", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        //protected static readonly Regex OldFileToPanelRegex = new Regex(@"\{\{(?'oldType'lan\|files)\|file\s*=\s*(?<file>/.[^\|]+)\s*\|+title\s*=\s*(?<title>.[^\|]+)\|+(\s*filesystem\s*=\s*true\s*\|+)*(viewtype\s*=\s*(?<viewtype>.*)\|+)*\}\}", RegexOptions.IgnoreCase);
        //protected static readonly Regex OldFolderToPanelRegex = new Regex(@"\{\{(?'oldType'lan\|files)\|file\s*=\s*(?<folder>[^\|\/]*)\s*\|+title\s*=\s*(?<title>.[^\|]+)\|+(\s*filesystem\s*=\s*true\s*\|+)*(viewtype\s*=\s*(?<viewtype>.*)\|+)*\}\}", RegexOptions.IgnoreCase);
        protected static readonly Regex OldFileItemToPanelRegex = new Regex(@"\{\{\s{0,}lan\|files\|file\s*=.+[^\}]\}\}", RegexOptions.IgnoreCase);

        protected const string OldFileToArticleRegexBeforeFile = @"\{\{\s{0,}(?'oldType'wiki\:){1}[/\\]{0,}";
        protected const string OldFileToArticleRegexAfterFile = @"[/\\]{0,}(\?.*?)*(\|(?'title'(\n|.)*?))*\s*\}\}";
        protected const string OldAudioVideoToArticleRegexBeforeFile = @"\{\{\s{0,}(?'oldType'lan\|audio|lan\|video)\|[/\\]{0,}";
        protected const string OldAudioVideoToArticleRegexAfterFile = @"(\?*\d{0,7}[px]*(\d{0,7}(px)*)*[/\\]{0,}\|(?'fs'(\s*filesystem\s*=\s*true\s*)*?))?(\|(?'title'(\n|.)*?))?\s*\}\}";
        protected const string OldFileToPanelRegexBeforeFile = @"\{\{\s{0,}(?'oldType'lan\|files)\|file\s*=\s*[/\\]{0,}";
        protected const string OldFileToPanelRegexAfterFile = @"[/\\]{0,}\s*\|+[^\}]+\}\}";

        protected const string PartsRegexString = @"\{\{lan\|part\|(?'data'.*?)\}\}";

        protected TableParser tableParser;

        protected DocumentStyler styler;

        protected AbstractCustomParser(DocumentStyler styler)
        {
            this.styler = styler;
            this.tableParser = new TableParser(styler);
        }

        public DocumentStyler GetStyler()
        {
            return styler;
        }

        /// <summary>
        /// Парсит списки в тексте. Должен вызываться в начале парсинга во избежание "поломок"
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual string ParseLists(string data)
        {
            //data = ParseLists(data, '*', "ul");
            //return ParseLists(data, '-', "ol");

            data = ParseLists(data, @"(^ {2,}\*(?'item'.*?)(?'tail'\|.*?)?$)", "ul");
            return ParseLists(data, @"(^ {2,}\-(?'item'.*?)(?'tail'\|.*?)?$)", "ol");
        }

        /// <summary>
        /// Проверяет, является ли статья "сводной" (то есть содержащей ссылки на другие статьи),
        /// удаляет эти ссылки и возвращает идекнтификаторы статей, входящих в данную
        /// </summary>
        /// <returns></returns>
        public static PartTagInfo[] GetPartPagesIds(ref string data)
        {
            var res = new List<PartTagInfo>();
            if (data == null)
                return res.ToArray();
            data = Regex.Replace(data, PartsRegexString, m =>
            {
                string part = m.Groups["data"].Value;
                // если идентификатора нет, то ищем название статьи
                Match titleMatch = Regex.Match(part, @"title\s*=\s*(?'title'.+?)\s*(\||$)", RegexOptions.IgnoreCase);
                // если нет и названия, тег считается нераспознанным
                if (!titleMatch.Success)
                    return m.ToString();
                string title = titleMatch.Groups["title"].Value;
                Match needTitleMatch = Regex.Match(part, @"showtitle\s*=\s*(?'flag'true|false)", RegexOptions.IgnoreCase);
                bool needTitle = needTitleMatch.Success && needTitleMatch.Groups["flag"].ToString().ToLower() == "true";

                Match colorMatch = Regex.Match(part, @"background\s*=\s*(?'color'.+?)\s*(\||$)", RegexOptions.IgnoreCase);
                string color = colorMatch.Success ? colorMatch.Groups["color"].ToString() : null;
                var toAdd = new PartTagInfo
                {
                    Title = title,
                    ShowTitle = needTitle,
                    Color = color
                };
                res.Add(toAdd);

                return toAdd.Replacement;
            }, RegexOptions.IgnoreCase);
            return res.ToArray();
        }

        public string RemoveLanPlugins(string data)
        {
            return Regex.Replace(data, @"{{lan\|(documents|objects|sources|attributes|thesaurus|files)\|(.*?)}}", "");
        }

        /// <summary>
        /// Ищет в тексте статьи "большие" заголовки, 
        /// которые превращаются в табы
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public PageSection[] FindSections(string data)
        {
            var res = new List<PageSection>();
            res.AddRange(PageSectionHelper.FindSections(data, HeaderType.Tab, 7));
            //res.AddRange(PageSectionHelper.FindSections(data, HeaderType.ListItem, 6));
            return res.ToArray();
        }

        private class ListItemSearchResult
        {
            public ListItemSearchResult()
            {
                IsListItem = false;
                Level = 1;
            }

            public ListItemSearchResult(int i)
            {
                Positon = i + 1;
                Level = i / 2;
                IsListItem = true;
            }

            public bool IsListItem { get; set; }

            public int Level { get; set; }

            public int Positon { get; set; }
        }

        private static ListItemSearchResult GetListItem(string str, char listToken)
        {
            int i = 0;
            while (i < str.Length && str[i] == ' ')
                i++;

            if (i < 1 || i + 1 > str.Length || str[i] != listToken)
                return new ListItemSearchResult();

            return new ListItemSearchResult(i);
        }

        private static StringBuilder AppendListTag(int current, int next, string tag, StringBuilder sb)
        {
            if (current == next)
                return sb.AppendFormat("</{0}>\r\n", tag);
            string toAppend = current > next ? string.Format("<{0}>", tag) : string.Format("</{0}></li>", tag);
            current = Math.Abs(current - next);
            while (current-- > 0)
                sb.Append(toAppend);

            return sb;
        }

        private static string ParseLists(string str, char itemToken, string tag)
        {
            if (str == null)
                return null;

            var res = new StringBuilder();
            string[] strings = str.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool isOpen = false;

            ListItemSearchResult itemInfo = null;
            ListItemSearchResult nextItemInfo = null;

            int prevLevel = 1;

            for (int index = 0; index < strings.Length; index++)
            {
                string item = strings[index];
                itemInfo = nextItemInfo == null || !isOpen ? GetListItem(item, itemToken) : nextItemInfo;
                if (!itemInfo.IsListItem)
                {
                    prevLevel = itemInfo.Level;
                    //if (isOpen)
                    //    res = AppendListTag(itemInfo.Level, prevLevel, tag, res);
                    res.AppendLine(item);
                    //res.AppendLine();
                    isOpen = false;
                    continue;
                }

                string content = item.Substring(itemInfo.Positon);
                if (!isOpen)
                {
                    res.AppendFormat("<{0}>", tag);
                    isOpen = true;
                }

                if (itemInfo.Level > prevLevel)
                {
                    res.Remove(res.Length - 5, 5);
                    res = AppendListTag(itemInfo.Level, prevLevel, tag, res);
                }

                nextItemInfo = strings.Length > index + 1 ? GetListItem(strings[index + 1], itemToken) : null;
                res.AppendFormat("<li>{0}</li>", content);

                if (nextItemInfo != null && (!nextItemInfo.IsListItem || itemInfo.Level > nextItemInfo.Level))
                    res = AppendListTag(nextItemInfo.Level, itemInfo.Level, tag, res);

                prevLevel = itemInfo.Level;
            }

            while (itemInfo != null && itemInfo.Level-- > 1)
                res.AppendFormat("</{0}>\r\n", tag);

            return res.ToString();
        }

        private string ParseLists(string data, string template, string itemTag)
        {
            //временно заменяем вертикальные черты в ссылках и тегах, 
            //так как они могут конфликтовать с символами таблиц
            const string replKey = "[[repl]]";
            string old = data;
            data = Regex.Replace(data, @"\[\[.*?(?'v1'\|).*?\]\]|{{.*?(?'v2'\|).*?}}", m => m.ToString().Replace("|", replKey), RegexOptions.Multiline);

            MatchCollection matches = Regex.Matches(data, template, RegexOptions.Multiline);

            if (matches.Count == 0)
                return old;

            var res = new StringBuilder();
            int currentLevel = 1;
            int lastMatchPos = -1;

            int iStart = 0, iEnd = 0;
            bool isOpened = false;

            bool isTable = false;
            string tail = null;

            foreach (Match match in matches)
            {
                iEnd = match.Groups[0].Index;
                if (iEnd != iStart + 1)
                {
                    if (isOpened)
                    {
                        for (int i = 0; i < currentLevel; i++)
                        {
                            res.AppendFormat("</{0}>{1}", itemTag, isTable ? tail : null);
                        }
                        if (isTable)
                            isTable = false;
                        isOpened = false;
                    }
                    res.Append(data.Substring(iStart, iEnd - iStart));
                }

                string trimmed = match.Groups[0].ToString().TrimStart('\n', '\r');
                int newLevel = (trimmed.Length - trimmed.TrimStart(' ').Length) / 2;
                if (!isOpened)
                {
                    if (itemTag == "ol")
                        res.AppendFormat("<{0}{1}>", itemTag, styler.OL); //class='custom-list'
                    else //ul
                        res.AppendFormat("<{0}{1}>", itemTag, styler.UL);

                    isOpened = true;
                }
                else if (newLevel > currentLevel)
                {
                    res.Remove(res.Length - 5, 5);
                    res.AppendFormat("<{0}>", itemTag);
                    currentLevel = newLevel;
                }
                else if (newLevel < currentLevel || (lastMatchPos >= 0))
                {
                    res.AppendFormat("</{0}>{1}", itemTag, isTable ? tail : null);
                    if (isTable)
                        isTable = false;
                    currentLevel = newLevel;
                    if (currentLevel == 0)
                        isOpened = false;
                }

                string item = match.Groups["item"].ToString().TrimEnd();
                if (match.Groups["tail"].Success)
                {
                    isTable = true;
                    tail = match.Groups["tail"].ToString().TrimEnd();
                }
                res.AppendFormat("<li{0}>{1}</li>", itemTag == "ol" ? styler.OlLi : styler.UlLi, item);
                iStart = iEnd + match.Length;
            }

            for (int i = 0; i < currentLevel; i++)
            {
                res.AppendFormat("</{0}>{1}", itemTag, isTable ? tail : null);
                if (isTable)
                    isTable = false;
            }
            // ообратно вставляем вертикальные черты 
            res.Append(data.Substring(Math.Max(iEnd, iStart)));
            return res.Replace(replKey, "|").ToString();
        }

        public string ParsePropertyTables(string data)
        {
            var res = new StringBuilder();

            int start, lastPosition = 0;

            while ((start = data.IndexOf(StartTableToken, lastPosition, StringComparison.Ordinal)) >= 0)
            {
                int end = data.IndexOf(EndTableToken, start + StartTableToken.Length, StringComparison.Ordinal);
                if (end == start || end < 0)
                    break;

                res.Append(data.Substring(lastPosition, start - lastPosition));

                lastPosition = end + EndTableToken.Length;
                string rawTable = data.Substring(start + StartTableToken.Length, end - start - StartTableToken.Length);

                res.Append(TableOpeningTag);

                var rows = rawTable.Split(new[] { RowTableToken }, StringSplitOptions.None);
                if (string.IsNullOrEmpty(rows.FirstOrDefault()))
                {
                    rows = rows.Skip(1).ToArray();
                }

                if (rows.Length <= 1)
                {
                    res = ParseTableRow(rows.FirstOrDefault(), res);
                    res.Append(TableClosingTag);
                    continue;
                }

                foreach (var row in rows)
                {
                    res = ParseTableRow(row, res);
                }

                res.Append(TableClosingTag);
            }

            res.Append(data.Substring(lastPosition));

            return res.ToString();
        }

        public string ParseTables(string data)
        {
            return tableParser.ParseTables(data);
        }

        public string ReplaceTags(string data)
        {
            data = Regex.Replace(data, @"<script(?'attr'\s*.*)>", "&lt;script${attr}>", RegexOptions.IgnoreCase);
            return Regex.Replace(data, @"<\/script>", "&lt;//script>", RegexOptions.IgnoreCase);
        }

        public string ParseStatisticsChart(string data)
        {
            IRepository repository = ObjectFactory.GetInstance<IRepository>();
            var portals = repository.GetPortals();

            data = Regex.Replace(data, @"{{lan\|(?'kind'(statistic|map))(?'data'(\n|.)*?)}}",
                m =>
                    {
                        var settings = ObjectFactory.GetInstance<ApplicationSettings>();
                        if (settings.PortableVersion)
                        {
                            return @"    <div class=""save_before_adding_files"">
        <div class=""attention_sign_div"">
        </div>
        <div>Используется портативная версия базы знаний. Функция недоступна.</div>
    </div>";
                        }

                        string kind = m.Groups["kind"].Value;
                        string raw = m.Groups["data"].Value;
                        string url = AttributeHelper.GetStringAttribute(raw, "url");
                        string title = AttributeHelper.GetStringAttribute(raw, "title");
                        string chartType = AttributeHelper.GetStringAttribute(raw, "chartType");
                        string portal = AttributeHelper.GetStringAttribute(raw, "portal");
                        int chartTypeId;
                        switch (chartType)
                        {
                            case "line":
                                chartTypeId = 1;
                                break;
                            case "pie":
                                chartTypeId = 2;
                                break;
                            default:
                                chartTypeId = 0;
                                break;
                        }

                        bool showTable = AttributeHelper.GetFlag(raw, "showTable");

                        string param = url.IndexOf('?') >=0 ? "&" : "?";
                        if (string.IsNullOrEmpty(portal))
                            return FormatPortalFrame(url, param, showTable, chartTypeId);

                        var portalName = portal;
                        var p = repository.GetPortals().FirstOrDefault(x => x.Name == portalName);
                        if (p == null)
                            return FormatPortalFrame(url, param, showTable, chartTypeId);

                        var portalUrl = p.Address;

                        if (portalUrl.EndsWith("/") && url.StartsWith("/"))
                        {
                            portalUrl = portalUrl.TrimEnd('/') + '/';
                            url = url.TrimStart('/');
                        }

                        if (!url.StartsWith("http"))
                            url = portalUrl + url;
                        else
                            url = ReplacePortalUrl(url, portalUrl);

                        return FormatPortalFrame(url, param, showTable, chartTypeId);
                    });

            return data;
        }

        /// <summary>
        /// Заменяет ссылку на ссылку из параметра portal.
        /// </summary>
        /// <param name="url">Исходный url.</param>
        /// <param name="portalUrl">Url портала.</param>
        /// <returns>Возвращает новый url объекта</returns>
        private string ReplacePortalUrl(string url, string portalUrl)
        {
            if (string.IsNullOrEmpty(portalUrl) || url.StartsWith(portalUrl))
                return url;

            var anchors = new []{ "Maps/Default.aspx", "Maps/MonitoringMap.aspx", "Statistics/Default.aspx" };
            foreach (var anchor in anchors)
            {
                var index = url.IndexOf(anchor, StringComparison.InvariantCultureIgnoreCase);

                if (index == -1)
                    continue;

                url = portalUrl + url.Substring(index);
                break;
            }

            return url;
        }

        /// <summary>
        /// Форматирует фрейм параметра lan|statistic или lan|map
        /// </summary>
        /// <param name="url">Url портала</param>
        /// <param name="param">Дополнительные параметры url</param>
        /// <param name="showTable">Показывать таблицу</param>
        /// <param name="chartTypeId">Id графика</param>
        /// <returns></returns>
        private string FormatPortalFrame(string url,string param, bool showTable, int chartTypeId)
        {
            return String.Format(@"<iframe class='lan-statistics-frame' src='{0}{1}showonlybody=1&showTableData={2}&chart={3}'></iframe>",
                url, param, showTable ? 1 : 0, chartTypeId);

        }

        private StringBuilder ParseTableRow(string rowData, StringBuilder sb)
        {
            rowData = rowData ?? "";
            bool isHeader = rowData.StartsWith(HeaderTableToken);
            sb.Append(TableRowOpeningTag);
            if (isHeader)
                rowData = rowData.Remove(0, HeaderTableToken.Length);

            var cells = rowData.Split(new[] { CellToken }, StringSplitOptions.None);
            const string alignToken = "  ";

            foreach (var cell in cells)
            {
                string cellContent = (String.IsNullOrEmpty(cell) ? "&nbsp;" : cell);
                string align = _alignParser.Parse(cellContent, alignToken);
                string span = _cellSpanParser.Parse(ref cellContent, ColspanRegex, RowspanRegex, ColspanReplacement, RowspanReplacement);

                string template = isHeader ? ThTemplateTag : TdTemplateTag; // ???
                sb.AppendFormat(template, String.IsNullOrEmpty(cell) ? " " : cellContent, align);
            }
            sb.Append(TableRowClosingTag(isHeader));

            return sb;
        }

        public static string ParseRedirect(string data, out bool hasRedirect)
        {
            Match match = Regex.Match(data, @"{{lan\|redirect\|\s*(?'page'.*?)\s*}}");
            hasRedirect = match.Success;
            return hasRedirect ? match.Groups["page"].ToString() : data;
        }

        public string ParseInfoTable(string data)
        {
            var sb = new StringBuilder();
            int start, lastPosition = 0;
            while ((start = data.IndexOf(StartInfoTableToken, lastPosition, StringComparison.Ordinal)) >= 0)
            {
                if (start < 0)
                    break;

                int end = InfotableEndPosition(data, start);
                if (end == start || end < 0)
                    break;

                sb.Append(data.Substring(lastPosition, start - lastPosition));
                lastPosition = end + EndInfoTableToken.Length;

                string rawTable = data.Substring(start + StartInfoTableToken.Length, end - start - StartInfoTableToken.Length);

                string header = null;
                if (rawTable.StartsWith(StartHeaderToken))
                {
                    int headerIndexEnd = rawTable.IndexOf(EndHeaderToken, StringComparison.Ordinal);
                    if (headerIndexEnd > 0 && headerIndexEnd < end)
                    {
                        header = rawTable.Substring(StartHeaderToken.Length, headerIndexEnd - StartHeaderToken.Length).Trim();
                        rawTable = rawTable.Remove(0, headerIndexEnd + EndHeaderToken.Length);
                    }
                }

                var cells = GetInfotableCells(rawTable);

                if (cells.Length <= 1)
                    break;

                sb.AppendFormat(InfoTableOpeningTag, header);
                int counter = 0;
                foreach (string cell in cells)
                {
                    string trTemplate;
                    if (IsImage(cell))
                        trTemplate = InfoTableImageTag;
                    else if (counter % 2 == 0)
                    {
                        trTemplate = InfoTableThTag;
                        counter++;
                    }
                    else
                    {
                        trTemplate = InfoTableTdTag;
                        counter++;
                    }
                    sb.AppendFormat(trTemplate, cell);
                }

                string endToken = cells.Length % 2 == 1 ? InfoTableOddClosingTag : InfoTableClosingTag;
                sb.Append(endToken);
            }
            sb.Append(data.Substring(lastPosition));
            return sb.ToString();
        }

        private static string[] GetInfotableCells(string rawCells)
        {
            if (String.IsNullOrWhiteSpace(rawCells))
                return new[] { rawCells };

            var res = new List<String>();
            int level = 0, previousPos = -1;

            int counter = 0;
            while (counter < rawCells.Length)
            {
                if (rawCells[counter] == '{' && rawCells[counter + 1] == '{')
                {
                    ++level;
                    counter++;
                }
                else if (rawCells[counter] == '}' && rawCells[counter + 1] == '}')
                {
                    --level;
                    counter++;
                }
                else if (rawCells[counter] == '[' && rawCells[counter + 1] == '[')
                {
                    ++level;
                    counter++;
                }
                else if (rawCells[counter] == ']' && rawCells[counter + 1] == ']')
                {
                    --level;
                    counter++;
                }
                else if (rawCells[counter] == CellToken && level == 0)
                {
                    res.Add(rawCells.Substring(previousPos >= 0 ? previousPos + 1 : 0, counter - previousPos - 1));
                    previousPos = counter;
                }
                counter++;
            }
            res.Add(rawCells.Substring(previousPos + 1));
            return res.ToArray();
        }

        private bool IsImage(string cell)
        {
            return ImageRegex.IsMatch(cell);
        }

        private static int InfotableEndPosition(string rawCells, int start)
        {
            int level = 0;

            int counter = start + StartTableToken.Length;
            while (counter < rawCells.Length)
            {
                if (rawCells.Length > counter + 1 && rawCells[counter] == '{' && rawCells[counter + 1] == '{')
                {
                    ++level;
                    counter++;
                }
                else if (rawCells.Length > counter + 1 && rawCells[counter] == '}' && rawCells[counter + 1] == '}')
                {
                    --level;
                    if (level == -1)
                        return counter;
                    counter++;
                }

                counter++;
            }
            return -1;
        }

        public virtual string ParseFooterNotes(string data)
        {
            const string noteAncor = "note";
            const string ancorInText = "note_top";
            int index = 0;
            var sb = new StringBuilder();

            var matches = new List<Match>();
            sb.Append(Regex.Replace(data, @"\(\((.*?)\)\)", match =>
            {
                matches.Add(match);
                return string.Format("<sup><a id='{2}_{0}' href='#{1}_{0}'>[{0}]</a></sup>", ++index, noteAncor, ancorInText);
            }));

            if (!matches.Any())
                return data;

            sb.AppendFormat("<div id='{1}'><h2>{0}</h2><ol>", "Примечания", "footnotes");

            index = 0;
            foreach (Match match in matches)
            {
                string noteStr = match.Groups[1].ToString();
                sb.AppendFormat("<li id='{2}_{1}'><a href='#{3}_{1}'>&uarr;&nbsp;</a>{0} </li>", noteStr, ++index, noteAncor, ancorInText);
            }

            sb.Append("</ol></div>");

            return sb.ToString();
        }

        public virtual string ParseBooksList(string data)
        {
            int index = 0;
            var sb = new StringBuilder();

            var matches = new List<Match>();
            sb.Append(Regex.Replace(data, @"{{lan\|book\|(.*?)}}", match =>
            {
                matches.Add(match);
                return String.Format("<a href='#{1}_{0}'>[{0}]</a>", ++index, "book");
            }));

            if (!matches.Any())
                return data;

            sb.AppendFormat("<div id='{1}'><h2>{0}</h2><ol>", "Список литературы", "bookslist");

            index = 0;
            foreach (Match match in matches)
            {
                string noteStr = match.Groups[1].ToString();
                sb.AppendFormat("<li id='book_{1}'>{0} </li>", noteStr, ++index);
            }

            sb.Append("</ol></div>");

            return sb.ToString();

            //return ParseNotes(data, "book", "<a href='#{1}_{0}'>[{0}]</a>", "Список литературы", "bookslist");
        }

        public string Justify(string data)
        {
            return Regex.Replace(data, @"<block justify>(?'text'.*?)<\/block>", JustifyReplacenment, RegexOptions.Singleline);
        }

        public string Colored(string data)
        {
            string colored = ColoredParser.ForegroundParser.Parse(data);
            return colored;
        }

        public string ColoredBackground(string data)
        {

            string colored = ColoredParser.BackgroundParser.Parse(data);
            return colored;
        }

        public string Strike(string data)
        {
            return Regex.Replace(data, @"<del>(.*?)<\/del>", @"<strike>$1</strike>", RegexOptions.Singleline);
        }

        public string Monospace(string data)
        {
            return Regex.Replace(data, @"''(.*?)''", "<span style='font-family: monospace;'>$1</span>", RegexOptions.Singleline);
        }

        public virtual string Underlined(string data)
        {
            return Regex.Replace(data, @"<ins>(.*?)<\/ins>", UnderlinedReplacenment, RegexOptions.Singleline);
        }

        public string Alerts(string data)
        {
            var templates = new Dictionary<string, string>
            {
                { @"{{lan\|warningbox\|(?'warningbox'.*?)}}", WarningReplacenment },
                { @"{{lan\|alertbox\|(?'alertbox'.*?)}}", AlertReplacenment },
                { @"{{lan\|infobox\|(?'infobox'.*?)}}", InfoboxReplacenment },
                { @"{{lan\|successbox\|(?'successbox'.*?)}}", SuccessboxReplacenment },
            };

            foreach (var alertsTemplate in templates)
            {
                data = Regex.Replace(data, alertsTemplate.Key, alertsTemplate.Value, RegexOptions.Singleline);
            }
            return data;
        }

        public string Video(string data)
        {
            return VideoRegex.Replace(data, VideoReplacement);
        }

        public string Audio(string data)
        {
            return AudioRegex.Replace(data, AudioReplacement);
        }

        #region Replacements

        protected abstract string JustifyReplacenment { get; }
        protected abstract string UnderlinedReplacenment { get; }
        protected abstract string WarningReplacenment { get; }
        protected abstract string AlertReplacenment { get; }
        protected abstract string InfoboxReplacenment { get; }
        protected abstract string SuccessboxReplacenment { get; }
        protected abstract string BackgroundReplacenment { get; }
        protected abstract string ColspanReplacement { get; }
        protected abstract string RowspanReplacement { get; }
        protected abstract string ColorReplacenment { get; }
        protected abstract string TableOpeningTag { get; }
        protected abstract string TableClosingTag { get; }
        protected abstract string TableRowOpeningTag { get; }
        protected abstract string TableRowClosingTag(bool isHeader);
        protected abstract string ThTemplateTag { get; }
        protected abstract string TdTemplateTag { get; }
        protected abstract string InfoTableOpeningTag { get; }
        protected abstract string InfoTableClosingTag { get; }
        protected abstract string InfoTableOddClosingTag { get; }
        protected abstract string InfoTableThTag { get; }
        protected abstract string InfoTableImageTag { get; }
        protected abstract string InfoTableTdTag { get; }
        protected abstract string VideoReplacement(Match m);
        protected abstract string AudioReplacement(Match m);

        #endregion
    }
}
