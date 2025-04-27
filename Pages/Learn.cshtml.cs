// filepath: e:\Visual Studio\Progetto\QueryMyst\Pages\Learn.cshtml.cs
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace QueryMyst.Pages
{
    public class LearnModel : PageModel
    {
        private readonly ILogger<LearnModel> _logger;

        public LearnModel(ILogger<LearnModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            _logger.LogInformation("Learn SQLite guide page accessed.");
        }
    }
}