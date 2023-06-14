#if EJ2_DNX
using System.Web;
#endif

namespace Samco_HSE_Manager.Base
{

    public class FileManagerResponse
    {
        public FileManagerDirectoryContent CWD { get; set; }
        public IEnumerable<FileManagerDirectoryContent> Files { get; set; }

        public ErrorDetails? Error { get; set; }

        public FileDetails Details { get; set; }

    }

}