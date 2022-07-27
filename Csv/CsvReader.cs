using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;

namespace Csv
{
    /// <summary>
    /// Helper class to read csv (comma separated values) data.
    /// </summary>
    public static class CsvReader
    {
        /// <summary>
        /// Reads the lines from the stream.
        /// </summary>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLine> ReadFromStream(Stream stream, CsvOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return ReadFromStreamImpl(stream, options);
        }
       
        /// <summary>
        /// Reads the lines from the stream.
        /// </summary>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="encoding">The stream encoding.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IAsyncEnumerable<ICsvLine> ReadFromStreamAsync(Stream stream, Encoding? encoding, CsvOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            static async IAsyncEnumerable<ICsvLine> Impl(Stream stream, Encoding? encoding, CsvOptions? options)
            {
                using var reader = new StreamReader(stream, encoding);
                await foreach (var line in ReadImplAsync(reader, options))
                    yield return line;
            }

            return Impl(stream, encoding, options);
        }

        private static IEnumerable<ICsvLine> ReadFromStreamImpl(Stream stream, CsvOptions? options)
        {
            using var reader = new StreamReader(stream);

            foreach (var line in ReadImpl(reader, options))
                yield return line;
        }

        private static IEnumerable<ICsvLine> ReadImpl(TextReader reader, CsvOptions? options)
        {
            // NOTE: Logic is copied in ReadImpl/ReadImplAsync/ReadFromMemory
            options ??= new CsvOptions();

            string? line;
            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;
            while ((line = reader.ReadLine()) != null)
            {
                index++;

                var lineAsMemory = line.AsMemory();
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(lineAsMemory, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(lineAsMemory.AsSpan(), options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(lineAsMemory, options) : CreateDefaultHeaders(lineAsMemory, options);

                    try
                    {
                        headerLookup = headers
                            .Select((h, idx) => Tuple.Create(h, idx))
                            .ToDictionary(h => h.Item1.AsString(), h => h.Item2, options.Comparer);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException("Duplicate headers detected in HeaderPresent mode. If you don't have a header you can set the HeaderMode to HeaderAbsent.");
                    }

                    var aliases = options.Aliases;
                    if (aliases != null)
                    {
                        // NOTE: For each group we need at most 1 match (i.e. SingleOrDefault)
                        foreach (var aliasGroup in aliases)
                        {
                            var groupIndex = -1;
                            foreach (var alias in aliasGroup)
                            {
                                if (headerLookup.TryGetValue(alias, out var aliasIndex))
                                {
                                    if (groupIndex != -1)
                                        throw new InvalidOperationException("Found multiple matches within alias group: " + string.Join(";", aliasGroup));

                                    groupIndex = aliasIndex;
                                }
                            }

                            if (groupIndex != -1)
                            {
                                foreach (var alias in aliasGroup)
                                    headerLookup[alias] = groupIndex;
                            }
                        }
                    }

                    if (skipInitialLine)
                        continue;
                }

                var record = new ReadLine(headers, headerLookup, index, line, options);
                if (options.AllowNewLineInEnclosedFieldValues)
                {
                    // TODO: Move to CsvLineSplitter?
                    // TODO: Shouldn't we only check the last part?
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
                    {
                        var nextLine = reader.ReadLine();
                        if (nextLine == null)
                            break;

                        line += options.NewLine + nextLine;
                        record = new ReadLine(headers, headerLookup, index, line, options);
                    }
                }

                yield return record;
            }
        }

        private static async IAsyncEnumerable<ICsvLine> ReadImplAsync(TextReader reader, CsvOptions? options)
        {
            // NOTE: Logic is copied in ReadImpl/ReadImplAsync/ReadFromMemory
            options ??= new CsvOptions();

            string? line;
            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                index++;

                var lineAsMemory = line.AsMemory();
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(lineAsMemory, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(lineAsMemory.Span, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(lineAsMemory, options) : CreateDefaultHeaders(lineAsMemory, options);

                    try
                    {
                        headerLookup = headers
                            .Select((h, idx) => (h, idx))
                            .ToDictionary(h => h.Item1.AsString(), h => h.Item2, options.Comparer);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException("Duplicate headers detected in HeaderPresent mode. If you don't have a header you can set the HeaderMode to HeaderAbsent.");
                    }

                    var aliases = options.Aliases;
                    if (aliases != null)
                    {
                        // NOTE: For each group we need at most 1 match (i.e. SingleOrDefault)
                        foreach (var aliasGroup in aliases)
                        {
                            var groupIndex = -1;
                            foreach (var alias in aliasGroup)
                            {
                                if (headerLookup.TryGetValue(alias, out var aliasIndex))
                                {
                                    if (groupIndex != -1)
                                        throw new InvalidOperationException("Found multiple matches within alias group: " + string.Join(";", aliasGroup));

                                    groupIndex = aliasIndex;
                                }
                            }

                            if (groupIndex != -1)
                            {
                                foreach (var alias in aliasGroup)
                                    headerLookup[alias] = groupIndex;
                            }
                        }
                    }

                    if (skipInitialLine)
                        continue;
                }

                var record = new ReadLine(headers, headerLookup, index, line, options);
                if (options.AllowNewLineInEnclosedFieldValues)
                {
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
                    {
                        var nextLine = await reader.ReadLineAsync();
                        if (nextLine == null)
                            break;

                        line += options.NewLine + nextLine;
                        record = new ReadLine(headers, headerLookup, index, line, options);
                    }
                }

                yield return record;
            }
        }

        private static MemoryText[] CreateDefaultHeaders(MemoryText line, CsvOptions options)
        {
            var columnCount = options.Splitter.Split(line, options).Count;

            var headers = new MemoryText[columnCount];
            for (var i = 0; i < headers.Length; i++)
                headers[i] = $"Column{i + 1}".AsMemory();

            return headers;
        }

        private static MemoryText[] GetHeaders(MemoryText line, CsvOptions options)
        {
            return Trim(SplitLine(line, options), options);
        }

        private static void InitializeOptions(SpanText line, CsvOptions options)
        {
            if (options.Separator == '\0')
                options.Separator = AutoDetectSeparator(line);

            options.Splitter = CsvLineSplitter.Get(options);
        }

        private static char AutoDetectSeparator(SpanText sampleLine)
        {
            // NOTE: Try simple 'detection' of possible separator
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var ch in sampleLine)
            {
                if (ch == ';' || ch == '\t')
                    return ch;
            }

            return ',';
        }

        private static IList<MemoryText> SplitLine(MemoryText line, CsvOptions options)
        {
            return options.Splitter.Split(line, options);
        }

        private static MemoryText[] Trim(IList<MemoryText> line, CsvOptions options)
        {
            var trimmed = new MemoryText[line.Count]; // TODO: Mutate existing array?
            for (var i = 0; i < line.Count; i++)
            {
                var str = line[i];
                if (options.TrimData)
                    str = str.Trim();

                if (str.Length > 1)
                {
                    if (str.Span[0] == '"' && str.Span[^1] == '"')
                    {
                        str = str[1..^1].Unescape('"', '"');

                        if (options.AllowBackSlashToEscapeQuote)
                            str = str.Unescape('\\', '"');
                    }
                    else if (options.AllowSingleQuoteToEncloseFieldValues && str.Span[0] == '\'' && str.Span[^1] == '\'')
                        str = str[1..^1];
                }

                trimmed[i] = str;
            }

            return trimmed;
        }

        private sealed class ReadLine : ICsvLine
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private readonly MemoryText[] headers;
            private IList<MemoryText>? rawSplitLine;
            internal MemoryText[]? parsedLine;

            public ReadLine(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, string raw, CsvOptions options)
            {
                this.headerLookup = headerLookup;
                this.options = options;
                this.headers = headers;
                Raw = raw;
                Index = index;
            }

            public string[] Headers => headers.Select(it => it.AsString()).ToArray();

            public string Raw { get; }

            public int Index { get; }

            public int ColumnCount => Line.Length;

            public bool HasColumn(string name) => headerLookup.ContainsKey(name);

            internal IList<MemoryText> RawSplitLine
            {
                get
                {
                    rawSplitLine ??= SplitLine(Raw.AsMemory(), options);
                    return rawSplitLine;
                }
            }

            public string[] Values => Line.Select(it => it.AsString()).ToArray();

            private MemoryText[] Line
            {
                get
                {
                    if (parsedLine == null)
                    {
                        var raw = RawSplitLine;

                        if (options.ValidateColumnCount && raw.Count != Headers.Length)
                            throw new InvalidOperationException($"Expected {Headers.Length}, got {raw.Count} columns.");

                        parsedLine = Trim(raw, options);
                    }

                    return parsedLine;
                }
            }

            string? ICsvLine.this[string name]
            {
                get
                {
                    if (!headerLookup.TryGetValue(name, out var index))
                    {
                        if (options.ReturnEmptyForMissingColumn)
                            return string.Empty;

                        if (options.ReturnNullForMissingColumn)
                            return null;

                        throw new ArgumentOutOfRangeException(nameof(name), name, $"Header '{name}' does not exist.");
                    }

                    try
                    {
                        if (options.ReturnResultWithTrim)
                            return Line[index].AsString().Trim();

                        return Line[index].AsString();
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException($"Invalid row, missing {name} header, expected {Headers.Length} columns, got {Line.Length} columns.");
                    }
                }
            }

            string ICsvLine.this[int index] => Line[index].AsString();

            public override string ToString()
            {
                return Raw;
            }
        }
    }
}