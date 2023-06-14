#if EJ2_DNX
using System.Web;
#endif

namespace Samco_HSE_Manager.Base
{
    public class ErrorDetails
    {

        public string Code { get; set; }

        public string Message { get; set; }

        public IEnumerable<string> FileExists { get; set; }
    }
}