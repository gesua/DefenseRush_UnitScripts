using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Defense.EditorTools
{
    /// <summary>
    /// 에디터 도구들이 함께 쓰는 CSV 표 읽기.
    /// 첫 줄이 열 이름이고, 따옴표로 감싼 칸 안의 쉼표와 "" 를 지원한다.
    /// </summary>
    public static class TableCsv
    {
        /// <summary>표의 한 줄.</summary>
        public class Row
        {
            private readonly Dictionary<string, int> _columns;
            private readonly List<string> _cells;

            public Row(int line, Dictionary<string, int> columns, List<string> cells)
            {
                Line = line;
                _columns = columns;
                _cells = cells;
            }

            /// <summary>파일에서 이 줄의 번호. (1부터 세고, 첫 줄은 열 이름이다)</summary>
            public int Line { get; }

            /// <summary>표에 이 열이 있는지. 선택 열을 건너뛸지 정할 때 쓴다.</summary>
            public bool HasColumn(string column) => _columns.ContainsKey(column);

            /// <summary>열 이름으로 칸의 값을 읽는다. 열이 없거나 칸이 비면 빈 문자열.</summary>
            public string Get(string column) =>
                (_columns.TryGetValue(column, out int index) && index < _cells.Count) ? _cells[index].Trim() : string.Empty;
        }

        /// <summary>
        /// 표의 모든 줄을 읽는다. 필수 열이 하나라도 없으면 problems에 적고 빈 목록을 돌려준다.
        /// </summary>
        public static List<Row> Read(string text, IEnumerable<string> requiredColumns, List<string> problems)
        {
            List<Row> rows = new List<Row>();
            string[] lines = text.TrimStart('\uFEFF').Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0])) return rows;

            Dictionary<string, int> columns = new Dictionary<string, int>();
            List<string> header = SplitLine(lines[0]);
            for (int i = 0; i < header.Count; i++) columns[header[i].Trim()] = i;

            foreach (string required in requiredColumns)
            {
                if (columns.ContainsKey(required) == false)
                {
                    problems.Add($"표에 '{required}' 열이 없습니다.");
                    return rows;
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                rows.Add(new Row(i + 1, columns, SplitLine(lines[i])));
            }
            return rows;
        }

        /// <summary>실수로 읽는다. 읽지 못하면 false.</summary>
        public static bool TryParseFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>실수로 읽는다. 비었거나 읽지 못하면 0.</summary>
        public static float ParseFloat(string text) => (TryParseFloat(text, out float value)) ? value : 0f;

        private static List<string> SplitLine(string line)
        {
            List<string> cells = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else current.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == ',') { cells.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            cells.Add(current.ToString());
            return cells;
        }
    }
}
