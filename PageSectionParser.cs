namespace CreoleParser.Sections
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Парсер секций страницы.
    /// </summary>
    public class PageSectionParser
    {
        /// <summary>
        /// Регулярные выражения для выявления секций.
        /// </summary>
        public static readonly Regex[] SectionsRegex =
        {
            new Regex("^(?<node>={7}(?<title>[^=]*)={7})", RegexOptions.Multiline),
            new Regex("^(?<node>={6}(?<title>[^=]*)={6})", RegexOptions.Multiline),
            new Regex("^(?<node>={5}(?<title>[^=]*)={5})", RegexOptions.Multiline),
            new Regex("^(?<node>={4}(?<title>[^=]*)={4})", RegexOptions.Multiline),
            new Regex("^(?<node>={3}(?<title>[^=]*)={3})", RegexOptions.Multiline),
            new Regex("^(?<node>={2}(?<title>[^=]*)={2})", RegexOptions.Multiline)
        };

        /// <summary>
        /// Разбирает содержимое страницы, возвращая ее корневую секцию.
        /// </summary>
        /// <param name="content">Содержимое страницы.</param>
        /// <returns>Корневая секция страницы.</returns>
        public PageSection Parse(string content)
        {
            var rootSection = new PageSection(0, 0, 0, string.Empty, 0) { Content = content };

            var path = new Stack<PageSection>();
            path.Push(rootSection);

            int level = 0;
            int position = 0;
            bool isLast = false;
            PageSection nextSection;

            while ((nextSection = FindNextSection(content, position, level, ref isLast)) != null)
            {
                var previousSection = path.Peek();
                int offsetFromPrevious = nextSection.Position - (previousSection.Position + previousSection.Length);

                foreach (var section in path)
                    section.Length += offsetFromPrevious;

                var parent = FindParentSection(path, nextSection);

                foreach (var section in path)
                    section.Length += nextSection.Length;

                if (!isLast)
                    parent.Children.Add(nextSection);
                else
                    previousSection.Length += nextSection.Length;

                path.Push(nextSection);

                position = nextSection.Position + nextSection.Length;
                level = nextSection.Level;
            }

            return rootSection;
        }

        /// <summary>
        /// Получает узел с преамблой к статье. Если преамбулы нет, возвращается null.
        /// </summary>
        /// <param name="content">Содержимое страницы.</param>
        /// <param name="rootSection">Корневая секция</param>
        /// <param name="hasToc">Есть ли в статье содержание или нет.</param>
        /// <param name="level">Уровень заголовка</param>
        /// <returns></returns>
        private PageSection GetPreamble(string content, PageSection rootSection, int level)
        {
            // преамбула есть в том случае, если между корневой секцией и следующей смещение равно нулю
            if (rootSection.Children.Count == 0 || rootSection.Position == rootSection.Children[0].Position)
                return null;

            var len = rootSection.Children[0].Position;
            var innerContent = content.Substring(0, len);
            return new PageSection(0, len, level, String.Empty, 0) { Content = innerContent };
        }

        /// <summary>
        /// Получает узел содержания.
        /// </summary>
        /// <param name="content">Содержимое страницы.</param>
        /// <param name="level">Уровень заголовка</param>
        /// <returns>Узел содержания.</returns>
        private PageSection GetToc(string content, int level)
        {
            return HasToc(content) ? new PageSection(0, 0, level, string.Empty, 0){Content = string.Empty} : null;
        }

        /// <summary>
        /// Проверяет, есть ли в статье содержание.
        /// </summary>
        /// <param name="content">Содержимое страницы.</param>
        /// <returns>Да или нет.</returns>
        private bool HasToc(string content)
        {
             return content.IndexOf("{NOTOC}", StringComparison.InvariantCultureIgnoreCase) == -1;
        }

        /// <summary>
        /// Добавляет секции содержания и преамбулы к документу.
        /// </summary>
        /// <param name="content">Содержимое страницы.</param>
        /// <param name="rootSection">Корневая секция</param>
        /// <returns>Корневой узел с добавленными вспомогательными узлами.</returns>
        public List<PageSection> AppendAdditionalSections(string content, PageSection rootSection)
        {
            var maxLevel = 0;
            if(rootSection.Children.Count > 0)
                maxLevel = rootSection.Children.Select(x => x.Level).ToArray().Min();

            AppendMissingTitle(rootSection);

            var hasToc = HasToc(content);
            var tocSection = GetToc(content, maxLevel);
            var preambleSection = GetPreamble(content, rootSection, maxLevel);

            var linearized = rootSection.Linearize();
            var result = new List<PageSection>();

            if (tocSection != null && linearized.Count != 0)
                result.Insert(0, tocSection);

            if(preambleSection != null)
                result.Insert(hasToc ? 1:0, preambleSection);

            // корректирую длинну секций, дабы
            // включать только тот текст, который между секциями
            // но исключить дочерние секции
            for(int i = 0;i< linearized.Count;i++)
            {
                var child = linearized[i];
                var length = child.Length;
                if (i > 0 && linearized[i - 1].Level < linearized[i].Level)
                {
                    // если текущий заголовок ниже предыдущего
                    // нужно подкорректировать длину предыдущего заголовка
                    var j = i;
                    length = linearized[i - 1].Length;
                    var level = linearized[i - 1].Level;
                    do
                    {
                        // учитываем только заголовки одного уровня
                        if (linearized[j].Level == level+1)
                        {
                            length -= linearized[j].Length;
                        }
                        j++;
                    }
                    // до тех пор, пока не выйдем на уровень выше текущего
                    // или не закончим список
                    while (j < linearized.Count && linearized[j].Level != level);

                    result[result.Count - 1].Length = length;
                }
                result.Add(new PageSection(child.Position, child.Length, child.Level, child.Title, child.BodyOffset));
            }

            if(linearized.Count == 0)
                result.Add(rootSection);

            return result;
        }

        private void AppendMissingTitle(PageSection rootSection)
        {
            foreach (var child in rootSection.Children)
            {
                if (string.IsNullOrEmpty(child.Title))
                    child.Title = "[Отсутствует текст заголовка]";

                AppendMissingTitle(child);
            }
        }

        /// <summary>
        /// Получает родительскую секцию для секции <paramref name="section"/>.
        /// </summary>
        /// <param name="path">Текущий путь поиска секций.</param>
        /// <param name="section">Секция, для которой необходимо найти родительский элемент.</param>
        /// <returns>Родительская секция; null, если такой секции нет.</returns>
        private PageSection FindParentSection(Stack<PageSection> path, PageSection section)
        {
            while (path.Any())
            {
                var topSection = path.Peek();

                if (topSection.Level < section.Level)
                    return topSection;

                path.Pop();
            }

            return null;
        }

        /// <summary>
        /// Ищет очередную секцию в тексте <paramref name="content"/>, начиная с позиции <paramref name="position"/>.
        /// </summary>
        /// <param name="content">Содержимое страницы.</param>
        /// <param name="position">Текущая позиция на странице.</param>
        /// <param name="level">Текущий уровень.</param>
        /// <param name="isLast">Признак является ли секция последней.</param>
        /// <returns>Найденная секция; null, если секция не найдена.</returns>
        private PageSection FindNextSection(string content, int position, int level, ref bool isLast)
        {
            if (position == content.Length)
                return null;

            int currentLevel = 0;
            int currentPosition = content.Length;
            Match currentMatch = null;

            for (int i = 0; i < SectionsRegex.Length; ++i)
            {
                var match = SectionsRegex[i].Match(content, position);
                if (match.Success && match.Index < currentPosition)
                {
                    currentLevel = i + 1;
                    currentPosition = match.Index;
                    currentMatch = match;
                }
            }

            isLast = currentMatch == null;
            var title = currentMatch != null ? currentMatch.Groups["title"].ToString().Trim() : string.Empty;
            var titleEnd = currentMatch != null ? currentMatch.Index + currentMatch.Length : 0;

            if (isLast)
                return new PageSection(position, content.Length - position, level, title, content.Length) { Content = content };

            return new PageSection(currentMatch.Index, currentMatch.Length, currentLevel, title, titleEnd) { Content = content };
        }
    }
}