using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;
using NUnit.Framework;
using SIL.LCModel;
using SIL.TestUtilities;
using TusDotNetClient;

namespace LfMerge.Core.Tests
{
	/// <summary>
	/// Test environment for end-to-end testing, i.e. Send/Receive with a real LexBox instance
	/// </summary>
	public class SRTestEnvironment
	{
		public ILogger Logger => MainClass.Logger;
		public Uri LexboxUrl { get; init; }
		public Uri LexboxUrlBasicAuth { get; init; }
		private TemporaryFolder TempFolder { get; init; }
		private HttpClient Http { get; init; }
		private HttpClientHandler Handler { get; init; } = new HttpClientHandler();
		private CookieContainer Cookies { get; init; } = new CookieContainer();
		private string Jwt { get; set; }

		public SRTestEnvironment(string lexboxHostname = "localhost", string lexboxProtocol = "http", int lexboxPort = 80, string lexboxUsername = "admin", string lexboxPassword = "pass")
		{
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPublicHostname, lexboxHostname);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotPrivateHostname, lexboxHostname);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_LanguageDepotUriProtocol, lexboxProtocol);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_HgUsername, lexboxUsername);
			Environment.SetEnvironmentVariable(MagicStrings.EnvVar_TrustToken, lexboxPassword);
			LexboxUrl = new Uri($"{lexboxProtocol}://{lexboxHostname}:{lexboxPort}");
			LexboxUrlBasicAuth = new Uri($"{lexboxProtocol}://{WebUtility.UrlEncode(lexboxUsername)}:{WebUtility.UrlEncode(lexboxPassword)}@{lexboxHostname}:{lexboxPort}");
			TempFolder = new TemporaryFolder(TestName + Path.GetRandomFileName());
			Handler.CookieContainer = Cookies;
			Http = new HttpClient(Handler);
		}

		public Task Login()
		{
			var lexboxUsername = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_HgUsername);
			var lexboxPassword = Environment.GetEnvironmentVariable(MagicStrings.EnvVar_TrustToken);
			return LoginAs(lexboxUsername, lexboxPassword);
		}

		public async Task LoginAs(string lexboxUsername, string lexboxPassword)
		{
			var loginResult = await Http.PostAsync(new Uri(LexboxUrl, "api/login"), JsonContent.Create(new { EmailOrUsername=lexboxUsername, Password=lexboxPassword }));
			var cookies = Cookies.GetCookies(LexboxUrl);
			Jwt = cookies[".LexBoxAuth"].Value;
			// Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Jwt);
			// Bearer auth on LexBox requires logging in to LexBox via their OAuth flow. For now we'll let the cookie container handle it.
		}

		public void InitRepo(string code, string dest)
		{
			var sourceUrl = new Uri(LexboxUrlBasicAuth, $"hg/{code}");
			MercurialTestHelper.CloneRepo(sourceUrl.AbsoluteUri, dest);
		}

		public void InitRepo(string code) => InitRepo(code, Path.Join(TempFolder.Path, code));

		public async Task ResetAndUploadZip(string code, string zipPath)
		{
			var resetUrl = new Uri(LexboxUrl, $"api/project/resetProject/{code}");
			await Http.PostAsync(resetUrl, null);
			await UploadZip(code, zipPath);
		}

		public async Task ResetToEmpty(string code)
		{
			var resetUrl = new Uri(LexboxUrl, $"api/project/resetProject/{code}");
			await Http.PostAsync(resetUrl, null);
			var finishResetUrl = new Uri(LexboxUrl, $"api/project/finishResetProject/{code}");
			await Http.PostAsync(finishResetUrl, null);
		}

		public async Task UploadZip(string code, string zipPath)
		{
			var sourceUrl = new Uri(LexboxUrl, $"/api/project/upload-zip/{code}");
			var file = new FileInfo(zipPath);
			var client = new TusClient();
			// client.AdditionalHeaders["Authorization"] = $"Bearer {Jwt}"; // Once we set up for LexBox OAuth, we'll use Bearer auth instead
			var cookies = Cookies.GetCookies(LexboxUrl);
			var authCookie = cookies[".LexBoxAuth"].ToString();
			client.AdditionalHeaders["cookie"] = authCookie;
			var fileUrl = await client.CreateAsync(sourceUrl.AbsoluteUri, file.Length, ("filetype", "application/zip"));
			await client.UploadAsync(fileUrl, file);
		}

		public async Task DownloadProjectBackup(string code)
		{
			var backupUrl = new Uri(LexboxUrl, $"api/project/backupProject/{code}");
			var result = await Http.GetAsync(backupUrl);
			var filename = result.Content.Headers.ContentDisposition?.FileName;
			var savePath = Path.Join(TempFolder.Path, filename);
			using (var outStream = File.Create(savePath))
			{
				await result.Content.CopyToAsync(outStream);
			}
		}

		public async Task RollbackProjectToRev(string code, int revnum)
		{
			// Negative rev numbers will be interpreted as Mercurial does: -1 is the tip revision, -2 is one back from the tip, etc.
			// I.e. rolling back to rev -2 will remove the most recent commit
			var backupUrl = new Uri(LexboxUrl, $"api/project/backupProject/{code}");
			var result = await Http.GetAsync(backupUrl);
			var zipStream = await result.Content.ReadAsStreamAsync();
			var projectDir = Path.Join(TempFolder.Path, code);
			ZipFile.ExtractToDirectory(zipStream, projectDir);
			var clonedDir = Path.Join(TempFolder.Path, $"{code}-{revnum}");
			MercurialTestHelper.CloneRepoAtRevnum(projectDir, clonedDir, revnum);
			var zipPath = Path.Join(TempFolder.Path, $"{code}-{revnum}.zip");
			ZipFile.CreateFromDirectory(clonedDir, zipPath);
			await ResetAndUploadZip(code, zipPath);
		}

		private string TestName
		{
			get
			{
				var testName = TestContext.CurrentContext.Test.Name;
				var firstInvalidChar = testName.IndexOfAny(Path.GetInvalidPathChars());
				if (firstInvalidChar >= 0)
					testName = testName.Substring(0, firstInvalidChar);
				return testName;
			}
		}

	}
}