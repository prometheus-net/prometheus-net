using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace Prometheus
{
	internal static class HttpRequestExtension
	{
		private const string UserAgent = "User-Agent";

		internal static bool IsCrawlerRequest(this HttpRequest request)
		{
			return !request.Headers.TryGetValue(UserAgent, out var userAgent) ||
				Regexes.Any(it => it.IsMatch(userAgent));
		}

		internal static bool IsCrawlerRequest(this HttpRequestMessage requestMessage)
		{
			return !requestMessage.Headers.TryGetValues(UserAgent, out var userAgentValues) ||
				userAgentValues.Any(it => Regexes.Any(reg => reg.IsMatch(it)));
		}

		private static IEnumerable<Regex> Regexes => _paterns.Select(it => new Regex(it, RegexOptions.Compiled));

		private static IReadOnlyCollection<string> _paterns = new[]
		{
			"Googlebot((\\-Image)|(\\-Mobile))?/(?'version'(?'major'\\d+)(?'minor'\\.\\d+)).*",
			"Mediapartners-Google",
			"AdsBot-Google",
			"archive.org_bot",
			"SAPO[+][-][+]Adwords",
			"Yandex",
			"SputnikBot",
			"Feed[Ff]{1}etcher-Google",
			"^Scooter(/|-)(?'version'(?'major'\\d+)(?'minor'\\.\\d+)).*",
			"Mercator",
			"Slurp",
			"MSNBOT",
			"^Gulliver/(?'version'(?'major'\\d+)(?'minor'\\.\\d+)).*",
			"ArchitextSpider",
			"Lycos_Spider",
			"Ask Jeeves",
			"^FAST-WebCrawler/(?'version'(?'major'\\d+)(?'minor'\\.\\d+)).*",
			"http\\:\\/\\/www\\.almaden.ibm.com\\/cs\\/crawler",
			"StackRambler",
			"YandexBot/(?'version'(?'major'\\d+)(?'minor'\\.\\d+)).*",
			"Twiceler",
			"ia_archiver",
			"METASpider",
			"Mail.RU",
			"AhrefsBot",
			"spider",
			"bingbot",
			"JoobleStateChecker",
			"^(Seznambot|SeznamBot)/(?'version'(?'major'\\d+)\\.(?'minor'\\d+)(-test)?).*"
		};
	}
}