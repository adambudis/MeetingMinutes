using Microsoft.AspNetCore.Mvc;

namespace MeetingMinutes.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(new { message = "API is running." });
    }
}
