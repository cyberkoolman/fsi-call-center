using Microsoft.AspNetCore.Mvc;

namespace RpContactCenter.Controllers
{
    [ApiController]
    [Route("")]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });
        }

        [HttpGet("")]
        public IActionResult Root()
        {
            return Ok(new 
            { 
                message = "RP Contact Center API - OpenAI Compatible",
                version = "1.0.0",
                endpoints = new[]
                {
                    "/v1/chat/completions",
                    "/v1/models",
                    "/health"
                }
            });
        }
    }
}
