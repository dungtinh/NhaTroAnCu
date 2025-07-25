using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(NhaTroAnCu.Startup))]
namespace NhaTroAnCu
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
