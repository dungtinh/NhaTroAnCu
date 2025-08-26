using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {

    }
}