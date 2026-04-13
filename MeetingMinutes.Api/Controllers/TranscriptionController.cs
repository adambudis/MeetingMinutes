using MeetingMinutes.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MeetingMinutes.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TranscriptionController(TranscriptionService transcription) : ControllerBase
    {
        [HttpPost]
        [RequestSizeLimit(500 * 1024 * 1024)] // 500 MB
        public async Task<IActionResult> Transcribe(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file is null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!TranscriptionService.IsAllowedExtension(ext))
                return BadRequest($"Unsupported file type '{ext}'. Allowed: .wav, .mp3");

            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");
            try
            {
                await using (var fs = System.IO.File.Create(tempFile))
                    await file.CopyToAsync(fs, cancellationToken);

                var result = await transcription.TranscribeAsync(tempFile, cancellationToken);
                return Ok(result);
            }
            catch (TranscriptionException ex)
            {
                return StatusCode(500, new { error = ex.Message, detail = ex.Detail });
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                    System.IO.File.Delete(tempFile);
            }
        }
    }
}
