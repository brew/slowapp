using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace slowapp
{
    public class RequestLogger
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public RequestLogger(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<RequestLogger>();
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            finally
            {
                _logger.LogInformation($"Incoming request at {context.Request.Path}, State: {State.GetState()}");
            }
        }
    }
}