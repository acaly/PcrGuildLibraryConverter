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
        InvalidHeaderFormat,
        InvalidTextFormat, //Not an error.
        CommentMultipleTarget,
        CommentInvalidTarget, //Not an error.
        DuplicateTeamId,
        NoAuthor, //Not an error.
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
