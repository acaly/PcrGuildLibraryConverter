using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.Data
{
    enum QQDocDownloadErrorType
    {
        ClrException,
        NetworkError,
        InvalidApiResponse,
        InvalidHeaderFormat,
        InvalidTextFormat,
        CommentMultipleTarget,
        CommentInvalidTarget,
        DuplicateTeamId,
    }

    class QQDocDownloadError
    {
        public QQDocDownloadErrorType ErrorType { get; init; }
        public string CellCoordinate { get; init; }
        public string CellText { get; init; }

        public string Title { get; init; }
        public string Details { get; init; }

        public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
    }

    class QQDocDownloadException : Exception
    {
        public QQDocDownloadError[] Errors { get; init; }
    }
}
