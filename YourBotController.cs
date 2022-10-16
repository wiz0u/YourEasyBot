#if ASPNETCORE // in a Webhook setup, remove this #if, and YourBot.Main method, and place this file in your Controllers folder
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace YourEasyBot.Controllers
{
	// This is a quick & dirty ASP.NET Core controller. In a serious environment, you would use IConfiguration, ILogger and a Singleton
	//  (teaching you how to correctly build an ASP.NET website is beyond the scope of this example)
	// Visit the controller base URL in a browser at least once to register the webhook

	[ApiController][Route("[controller]")]
	public class YourBotController : ControllerBase
	{
		const string BOT_TOKEN = "PASTE_YOUR_BOT_TOKEN_HERE";
		static YourBot bot;

		public YourBotController() => bot ??= new YourBot(BOT_TOKEN);

		[HttpGet]
		public Task<string> Get() => bot.CheckWebhook($"{Request.Scheme}://{Request.Host}{Request.Path}?token={BOT_TOKEN}");

		[HttpPost]
		public IActionResult Post(string token, [FromBody] Telegram.Bot.Types.Update update)
		{
			if (token != BOT_TOKEN) return Forbid();
			bot.HandleUpdate(update);
			return Ok();
		}
	}
}
#endif