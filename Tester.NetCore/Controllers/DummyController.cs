using Microsoft.AspNetCore.Mvc;

namespace tester
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
