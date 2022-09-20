using Microsoft.AspNetCore.Mvc;

namespace tester.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class DummyController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            return "Hello tester";
        }
    }
}
