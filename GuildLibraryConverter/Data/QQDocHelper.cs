using CefSharp;
using CefSharp.Handler;
using CefSharp.OffScreen;
using CefSharp.Web;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace GuildLibraryConverter.Data
{
    static class QQDocHelper
    {
        private class QQDocDownloadHandler : ResourceRequestHandler, IResourceRequestHandlerFactory
        {
            private class ResourceFilter : IResponseFilter
            {
                public string Url { get; init; }
                public bool IsOpenDocApi { get; init; }
                private readonly MemoryStream _ms = new();
                private readonly TaskCompletionSource<(string Url, bool IsOpenDocApi, MemoryStream Stream)> _taskSource = new();

                public Task<(string Url, bool IsOpenDocApi, MemoryStream Stream)> Task => _taskSource.Task;

                public bool InitFilter()
                {
                    return true;
                }

                public void Dispose()
                {
                    //Received the entire response now. Return it asynchronously.
                    _taskSource.SetResult((Url, IsOpenDocApi, _ms));
                }

                public FilterStatus Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten)
                {
                    if (dataIn == null)
                    {
                        dataInRead = 0;
                        dataOutWritten = 0;
                        return FilterStatus.Done;
                    }

                    //Calculate how much data we can read, in some instances dataIn.Length is
                    //greater than dataOut.Length
                    dataInRead = Math.Min(dataIn.Length, dataOut.Length);
                    dataOutWritten = dataInRead;
                    int intReadLength = (int)dataInRead;

                    var readBytes = ArrayPool<byte>.Shared.Rent(intReadLength); //new byte[dataInRead];
                    dataIn.Read(readBytes, 0, intReadLength);
                    dataOut.Write(readBytes, 0, intReadLength);
                    _ms.Write(readBytes, 0, intReadLength);
                    ArrayPool<byte>.Shared.Return(readBytes);

                    //If we read less than the total amount avaliable then we need
                    //return FilterStatus.NeedMoreData so we can then write the rest
                    if (dataInRead < dataIn.Length)
                    {
                        return FilterStatus.NeedMoreData;
                    }

                    return FilterStatus.Done;
                }
            }

            private readonly ConcurrentQueue<Task<(string Url, bool IsOpenDocApi, MemoryStream Stream)>> _responseTasks = new();

            //Input.
            private readonly string[] _authors;

            //Temporary lists.
            private readonly Dictionary<(int Row, int Column), string> _images = new();
            private readonly List<(int Row, Team Team)> _teams = new();

            private readonly Dictionary<(int Row, int Column), Source> _sources = new();
            private readonly List<(int Row, int Column, string Text)> _comments = new();
            private readonly List<((int Row, int Column)[], string Text)> _multiComments = new();

            //Outputs.
            public List<Team> Results { get; } = new();
            public List<QQDocDownloadError> Errors { get; } = new();
            public List<(string Url, string Filename, byte[] Data)> RawData { get; } = new();

            public QQDocDownloadHandler(string authorFile)
            {
                _authors = File.ReadAllLines(authorFile);
            }

            private int BinarySearchTeamByRow(int row)
            {
                int low = 0, high = _teams.Count;
                while (low < high - 1)
                {
                    var mid = low + (high - low) / 2;
                    if (_teams[mid].Row > row)
                    {
                        high = mid;
                    }
                    else
                    {
                        low = mid;
                    }
                }
                return low;
            }

            private void ResolveSourceParents()
            {
                _teams.Sort((l1, l2) => l1.Row - l2.Row);
                foreach (var ((row, col), source) in _sources)
                {
                    _teams[BinarySearchTeamByRow(row)].Team.Sources.Add(source);
                }
            }

            private void ResolveCommentReferences()
            {
                foreach (var (row, column, text) in _comments)
                {
                    if (_sources.TryGetValue((row, column), out var source))
                    {
                        source.Comments.Add(text);
                    }
                    else
                    {
                        //No longer consider this as an error.
                        //Errors.Add(new QQDocDownloadError
                        //{
                        //    ErrorType = QQDocDownloadErrorType.CommentInvalidTarget,
                        //    CellCoordinate = MakeCellCoordinate(row, column),
                        //    Title = "原始数据格式错误：带箭头的备注指向无效的单元格",
                        //    Details = text,
                        //});
                    }
                }
                foreach (var (targets, text) in _multiComments)
                {
                    Source source = null;
                    foreach (var (row, col) in targets)
                    {
                        if (_sources.TryGetValue((row, col), out var newSource))
                        {
                            if (source is null)
                            {
                                source = newSource;
                            }
                            else
                            {
                                Errors.Add(new QQDocDownloadError
                                {
                                    ErrorType = QQDocDownloadErrorType.CommentMultipleTarget,
                                    CellCoordinate = string.Join(", ", targets.Select(t => MakeCellCoordinate(t.Row, t.Column))),
                                    Title = "原始数据格式错误：带箭头的备注指向多个的单元格",
                                    Details = text,
                                });
                                source = null;
                                break;
                            }
                        }
                    }
                    if (source is not null)
                    {
                        source.Comments.Add(text);
                    }
                }
            }

            private static readonly HttpClient _imageDownloadClient = new();
            private static readonly Dictionary<string, string> _imgExtensionMap = new()
            {
                { "image/bmp", ".bmp" },
                { "image/jpeg", ".jpg" },
                { "image/pict", ".pic" },
                { "image/png", ".png" },
                { "image/x-png", ".png" },
                { "image/tiff", ".tiff" },
                { "image/x-macpaint", ".mac" },
                { "image/x-quicktime", ".qti" },
            };

            private async Task DownloadImages()
            {
                if (Errors.Count > 0)
                {
                    //We have errors here. Just erase all image data.
                    foreach (var s in Results.SelectMany(l => l.Sources))
                    {
                        for (int i = 0; i < s.Images.Count; ++i)
                        {
                            s.Images[i] = null;
                        }
                    }
                }
                else
                {
                    //Concurrently download all images and encode as base64.
                    async Task DownloadImageListAsync(string id, Source source)
                    {
                        var list = source.Images;
                        for (int i = 0; i < list.Count; ++i)
                        {
                            try
                            {
                                var response = await _imageDownloadClient.GetAsync(list[i]);
                                var responseType = response.Content.Headers.ContentType;
                                var data = await response.Content.ReadAsByteArrayAsync();
                                if (!_imgExtensionMap.TryGetValue(responseType.MediaType, out var ext))
                                {
                                    ext = ".dat";
                                }
                                var hash = string.Concat(MD5.HashData(data).Select(d => d.ToString("X2", CultureInfo.InvariantCulture)));
                                var filename = $"img-{id}-{hash}{ext}";
                                lock (RawData)
                                {
                                    RawData.Add((list[i], filename, data));
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    await Task.WhenAll(Results
                        .SelectMany(l => l.Sources.Select(s => (Id: l.Id, Source: s)))
                        .Where(s => s.Source.Images.Count > 0)
                        .Select(s => DownloadImageListAsync(s.Id, s.Source)));
                }
            }

            private static string MakeCellCoordinate(int row, int column)
            {
                if (column > 25)
                {
                    return $"({row}, {column})";
                }
                return $"{(char)('A' + column)}{row + 1}";
            }

            private int _currentRow = -1;
            private Team _currentTeam = new();

            private static readonly Regex _damageRangeW = new Regex("(\\d+)[~-](\\d+)w");
            private static readonly Regex _damageValueW = new Regex("(\\d+)w");

            private bool ParseSourceDescriptionText(string description, Source source, int row, int col)
            {
                string author = null;
                foreach (var a in _authors)
                {
                    if (description.Contains(a, StringComparison.Ordinal))
                    {
                        author = a;
                        break;
                    }
                }
                if (author is null)
                {
                    //Don't add as error.
                    //Errors.Add(new QQDocDownloadError
                    //{
                    //    ErrorType = QQDocDownloadErrorType.NoAuthor,
                    //    Title = "无法识别作者名",
                    //    Details = description,
                    //    CellText = description,
                    //    CellCoordinate = MakeCellCoordinate(row, col),
                    //});
                }
                source.Author = author;

                Range range;
                var matchRange = _damageRangeW.Match(description);
                if (matchRange.Success)
                {
                    range = new()
                    {
                        Low = 10000 * int.Parse(matchRange.Groups[1].Value, CultureInfo.InvariantCulture),
                        High = 10000 * int.Parse(matchRange.Groups[2].Value, CultureInfo.InvariantCulture),
                    };
                }
                else
                {
                    var matchValue = _damageValueW.Match(description);
                    if (matchValue.Success)
                    {
                        range = new() { Value = 10000 * int.Parse(matchValue.Groups[1].Value, CultureInfo.InvariantCulture) };
                    }
                    else
                    {
                        return false;
                    }
                }
                source.Damage = range;

                source.Description = description;
                return true;
            }

            private void TryAddComment(string description, int row, int col)
            {
                int count = 0;
                bool up = description.Contains('↑', StringComparison.Ordinal);
                bool down = description.Contains('↓', StringComparison.Ordinal);
                bool left = description.Contains('←', StringComparison.Ordinal);
                bool right = description.Contains('→', StringComparison.Ordinal);
                var targetRow = row;
                var targetCol = col;
                if (up)
                {
                    count += 1;
                    targetRow -= 1;
                }
                if (down)
                {
                    count += 1;
                    targetRow += 1;
                }
                if (left)
                {
                    count += 1;
                    targetCol -= 1;
                }
                if (right)
                {
                    count += 1;
                    targetCol += 1;
                }

                if (count == 1)
                {
                    _comments.Add((targetRow, targetCol, description));
                }
                else if (count > 1)
                {
                    //Pointing to multiple cells. Store this in _multiComments list and resolve later.
                    var targets = new (int Row, int Column)[count];
                    int i = 0;
                    if (up) targets[i++] = (row - 1, col);
                    if (down) targets[i++] = (row + 1, col);
                    if (left) targets[i++] = (row, col - 1);
                    if (right) targets[i++] = (row, col + 1);
                    _multiComments.Add((targets, description));

                    //No longer considered as an error.
                    //Errors.Add(new QQDocDownloadError
                    //{
                    //    ErrorType = QQDocDownloadErrorType.CommentMultipleTarget,
                    //    CellCoordinate = MakeCellCoordinate(row, col),
                    //    Title = "原始数据格式错误：带箭头的备注指向多个单元格",
                    //    Details = description,
                    //});
                    //return false;
                }
            }

            private void ProcessCellElement(int row, int col, JsonElement element)
            {
                var text = element.TryGetProperty("2", out var textElement) ? textElement[1].ToString() : null;
                var link = element.TryGetProperty("6", out var linkElement) ? linkElement.GetString() : null;
                var underline =
                    element.TryGetProperty("8", out var styleElement) &&
                    styleElement.ValueKind == JsonValueKind.Array && styleElement.GetArrayLength() > 0 &&
                    styleElement[0].ValueKind == JsonValueKind.Array && styleElement[0].GetArrayLength() > 1 &&
                    styleElement[0][1].TryGetProperty("6", out var underlineElement) &&
                    underlineElement.TryGetInt32(out var ul) ? ul == 1 : false;
                if (underline)
                {

                }

                bool hasText = !string.IsNullOrWhiteSpace(text);
                switch (col)
                {
                case 0: //Boss name.
                    if (hasText)
                    {
                        _currentRow = row;
                        _currentTeam = new Team
                        {
                            Boss = text,
                            Time = new Range { Value = 90 },
                        };
                    }
                    break;
                case 1: //Team id.
                    if (hasText)
                    {
                        _currentTeam.Id = text;
                        //Id containing 't' (e.g. ct101) is auto.
                        if (text.Contains('t', StringComparison.OrdinalIgnoreCase))
                        {
                            _currentTeam.Labels.Add("Auto");
                        }
                    }
                    break;
                case 2: //Characters
                case 3:
                case 4:
                case 5:
                case 6:
                    if (hasText)
                    {
                        _currentTeam.Characters.Add(new Character()
                        {
                            Id = CharacterIdConversion.GetCharacterId(text),
                        });
                    }
                    break;
                case 7: //Damage
                    if (hasText && text[^1] == 'w' && int.TryParse(text[..^1], out var damageW))
                    {
                        _currentTeam.StandardDamage = damageW * 10000;
                        if (_currentTeam.Boss is not null &&
                            _currentTeam.Id is not null &&
                            _currentTeam.StandardDamage != 0 &&
                            _currentTeam.Characters.Count == 5)
                        {
                            //All fields are valid. Add it as a new team.
                            _teams.Add((row, _currentTeam));
                            Results.Add(_currentTeam);
                        }
                        else if (_currentTeam.Boss is not null ||
                            _currentTeam.Id is not null ||
                            _currentTeam.StandardDamage != 0 ||
                            _currentTeam.Characters.Count == 5)
                        {
                            //Some, but not all fields are set. This is an error.
                            List<string> errorDetails = new();
                            if (_currentTeam.Boss is null)
                            {
                                errorDetails.Add("Boss名为空");
                            }
                            if (_currentTeam.Id is null)
                            {
                                errorDetails.Add("轴名为空");
                            }
                            if (_currentTeam.StandardDamage == 0)
                            {
                                errorDetails.Add("伤害为空");
                            }
                            if (_currentTeam.Characters.Count != 5)
                            {
                                errorDetails.Add("角色数不为5");
                            }
                            Errors.Add(new QQDocDownloadError
                            {
                                ErrorType = QQDocDownloadErrorType.InvalidHeaderFormat,
                                CellCoordinate = MakeCellCoordinate(row, 0),
                                Title = "原始数据格式错误：轴表基本数据格式错误",
                                Details = string.Join('\n',errorDetails),
                            });
                        }
                    }
                    break;
                case 8: //Description
                    if (hasText)
                    {
                        _currentTeam.Comments.Add(text);
                    }
                    break;
                case > 9:
                    //Sources
                    if (hasText &&
                        !string.IsNullOrWhiteSpace(link) &&
                        !_images.ContainsKey((row, col)))
                    {
                        //Has text and link but no image. This is a valid normal source.
                        var source = new Source();
                        if (ParseSourceDescriptionText(text, source, row, col))
                        {
                            source.Links.Add(link);
                            _sources.Add((row, col), source);
                        }
                    }
                    else if (hasText &&
                        string.IsNullOrWhiteSpace(link) &&
                        _images.TryGetValue((row, col), out var imageUrl))
                    {
                        //Has text and image but no link. This is a valid image source.
                        var source = new Source();
                        if (ParseSourceDescriptionText(text, source, row, col))
                        {
                            source.Images.Add(imageUrl);
                            _sources.Add((row, col), source);
                        }
                    }
                    else if (hasText && underline)
                    {
                        //Has text and text is manually set with underline style. Add as source.
                        var source = new Source();
                        if (ParseSourceDescriptionText(text, source, row, col))
                        {
                            _sources.Add((row, col), source);
                        }
                    }
                    else if (hasText)
                    {
                        TryAddComment(text, row, col);
                    }
                    break;
                }
            }

            private void ProcessImageElement(JsonElement element)
            {
                var url = element[3].GetString();
                var col = element[6].GetInt32();
                var row = element[7].GetInt32();
                _images[(row, col)] = url;
            }

            private void ProcessApiResponse(JsonElement element)
            {
                JsonElement cellData = default;
                foreach (var ea in element.EnumerateArray())
                {
                    foreach (var e in ea.EnumerateArray())
                    {
                        var type = e.GetProperty("t").GetInt32();
                        if (type == 3)
                        {
                            cellData = e.GetProperty("c");
                        }
                        else if (type == 8)
                        {
                            ProcessImageElement(e.GetProperty("c"));
                        }
                    }
                }

                {
                    var c = cellData;
                    var range = c[0];
                    var tabName = range[0].GetString();
                    var (rowMin, rowMax) = (range[1].GetInt32(), range[2].GetInt32());
                    var (colMin, colMax) = (range[3].GetInt32(), range[4].GetInt32());
                    var colCount = colMax - colMin + 1;
                    var content = c[1];
                    foreach (var p in content.EnumerateObject())
                    {
                        var index = int.Parse(p.Name, CultureInfo.InvariantCulture);
                        var rowIndex = index / colCount + rowMin;
                        var colIndex = index % colCount + colMin;
                        ProcessCellElement(rowIndex, colIndex, p.Value);
                    }
                }
            }

            private void ProcessOpenDocApi(MemoryStream ms)
            {
                var responseStr = Encoding.UTF8.GetString(ms.ToArray());
                //This will cause deadlock. Need to return the resource first
                //(start a task completion source and let caller to wait there)
                //if this works, the Init in CVCconv can be removed
                var jsonStr = ClientVarsCallbackConversion.ConvertAsync(responseStr).Result;
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                var data = root.GetProperty("clientVars").GetProperty("collab_client_vars");
                var text = data.GetProperty("initialAttributedText").GetProperty("text");
                ProcessApiResponse(text[0]);
            }

            private void ProcessSheetApi(MemoryStream ms)
            {
                ms.Seek(0, SeekOrigin.Begin);
                using var doc = JsonDocument.Parse(ms);
                var root = doc.RootElement;
                var data = root.GetProperty("data");
                var text = data.GetProperty("initialAttributedText").GetProperty("text");
                ProcessApiResponse(text[0]);
            }

            private void CheckTeamIds()
            {
                if (_teams.Select(l => l.Team.Id).Distinct().Count() != _teams.Count)
                {
                    //Check fails, but we need to find the items with duplicate ids.
                    var group = _teams.GroupBy(l => l.Team.Id).First(g => g.Count() > 1);
                    var groupPositions = string.Join(", ", group.Select(l => MakeCellCoordinate(l.Row, 0)));
                    Errors.Add(new QQDocDownloadError
                    {
                        ErrorType = QQDocDownloadErrorType.DuplicateTeamId,
                        CellCoordinate = groupPositions,
                        Title = "原始数据格式错误：轴名重复",
                        Details = "位置：" + groupPositions,
                    });
                }
            }

            public volatile string LoadingStageDescription = "准备";

            public async Task Load(string url)
            {
                LoadingStageDescription = "渲染万用表页面";
                var browser = new ChromiumWebBrowser();
                browser.ResourceRequestHandlerFactory = this;
                while (!browser.IsBrowserInitialized)
                {
                    await Task.Delay(100);
                }
                await browser.LoadUrlAsync(url);
                while (browser.IsLoading)
                {
                    await Task.Delay(100);
                }

                browser.Dispose();

                LoadingStageDescription = "解析腾讯文档接口返回内容";
                var allResponses = await Task.WhenAll(_responseTasks);
                int requestIndex = 0;
                foreach (var (apiUrl, isOpenDocApi, stream) in allResponses)
                {
                    if (isOpenDocApi)
                    {
                        ProcessOpenDocApi(stream);
                        RawData.Add((apiUrl, $"request{requestIndex++}.js", stream.ToArray()));
                    }
                    else
                    {
                        ProcessSheetApi(stream);
                        RawData.Add((apiUrl, $"request{requestIndex++}.json", stream.ToArray()));
                    }
                }

                //Organize teams and sources after all responses have been handled.
                LoadingStageDescription = "生成json数据";
                CheckTeamIds();
                ResolveSourceParents();
                ResolveCommentReferences();

                //Download images. This is done after loading the page in order to minimize delay of the loading.
                LoadingStageDescription = "下载图片";
                await DownloadImages();
                LoadingStageDescription = "完成";
            }

            bool IResourceRequestHandlerFactory.HasHandlers => true;

            public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser,
                IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload,
                string requestInitiator, ref bool disableDefaultHandling)
            {
                return this;
            }

            protected override IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser,
                IBrowser browser, IFrame frame, IRequest request, IResponse response)
            {
                if (request.Url.StartsWith("https://docs.qq.com/dop-api/opendoc", StringComparison.Ordinal))
                {
                    var f = new ResourceFilter() { Url = request.Url, IsOpenDocApi = true };
                    _responseTasks.Enqueue(f.Task);
                    return f;
                }
                else if (request.Url.StartsWith("https://docs.qq.com/dop-api/get/sheet", StringComparison.Ordinal))
                {
                    var parameters = HttpUtility.ParseQueryString(new Uri(request.Url).Query);
                    string tab = parameters.Get("tab");
                    string subId = parameters.Get("subId");
                    if (tab == subId)
                    {
                        var f = new ResourceFilter() { Url = request.Url, IsOpenDocApi = false };
                        _responseTasks.Enqueue(f.Task);
                        return f;
                    }
                    return null;
                }
                return null;
            }
        }

        public static async Task<(List<Team> Data, List<(string Url, string Filename, byte[] Response)> RawData)> DownloadFromQQDocument(string url)
        {
            var handler = new QQDocDownloadHandler("authors.txt");

            try
            {
                await handler.Load(url);
            }
            catch (Exception e)
            {
                handler.Errors.Add(new QQDocDownloadError
                {
                    ErrorType = QQDocDownloadErrorType.ClrException,
                    Title = $"{handler.LoadingStageDescription}时发生错误：{e.Message}",
                    Details = e.StackTrace,
                });
            }

            if (handler.Errors.Count > 0)
            {
                throw new QQDocDownloadException { Errors = handler.Errors.ToArray() };
            }
            return (handler.Results, handler.RawData);
        }
    }
}
