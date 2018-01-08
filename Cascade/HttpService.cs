//using System;
//using System.Net.Http;
//using Newtonsoft.Json;
//using System.Threading.Tasks;
//using System.Net.Http.Headers;
//using System.Text;
////using Plugin.Connectivity;
//using Serilog;
//using System.Collections;
//using System.Collections.Generic;
//
//namespace Cascade
//{
//
//	// from https://raw.githubusercontent.com/xamarin/MyCompany/master/SharedCode/MyCompany.Visitors.Client/Web/BaseRequest.cs
//	public class HttpService {
//
//		public HttpService() {
//			JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
//				NullValueHandling = NullValueHandling.Ignore,
//				ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
//				//Formatting = Formatting.Indented,
//				//TypeNameHandling = TypeNameHandling.Objects,
//				//ContractResolver = new CamelCasePropertyNamesContractResolver()
//			};
//
//			var jsonSerialization = new BzJsonSerialization();
//			jsonSerialization.initialise(new CascadeModelJsonConverter());
//			jsonSerialization.initialise(new LawAbidingFloatConverter());
//			Json = jsonSerialization;	
//		}
//
//		public BzJsonSerialization Json { get; private set; }
//
//		// When used directly, the WrapperException itself should be ignored - the real exception is the inner one.
//		// This pattern is necessary to retain the stack trace when rethrowing
//		
//		//!!! should use AggregateException
//		public class WrapperException : Exception {
//			public WrapperException (string aMessage, Exception aInnerException) : base (aMessage,aInnerException) {
//			}
//		}
//		
//		public class UnauthorizedException : Exception {
//			public UnauthorizedException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//
//		public class ForbiddenException : Exception {
//			public ForbiddenException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//
//		public class NotFoundException : Exception {
//			public NotFoundException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//
//		public class NoNetworkException : Exception {
//			public NoNetworkException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//
//		public class ServerErrorException : Exception {
//			public ServerErrorException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//
//		public class InternalErrorException : Exception {
//			public InternalErrorException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//		
//		public class UnsuccessfulException : Exception {
//			public UnsuccessfulException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//		
//		public class UserErrorException : Exception {
//			public UserErrorException (string aMessage, Exception aInnerException=null) : base (aMessage,aInnerException) {
//			}
//		}
//		
//		public static void EnsureNetwork () {
////			if (!CrossConnectivity.Current.IsConnected)
////				throw new NoNetworkException ("Not connected to the internet");
//		}
//
//		public static void EnsureAuthorized (HttpResponseMessage aResponse, string aMessage=null) {
//			if (aResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
//				throw new UnauthorizedException (aMessage ?? "Not Authenticated");
//		}
//
//		public static async Task<String> getContent (HttpResponseMessage aHRM) {
//			var body_s = await aHRM.Content.ReadAsStringAsync();
//			if (body_s == null)
//				return null;
//			body_s = body_s.Trim(new char[]{'\uFEFF','\u200B'});
//			return body_s;
//		}
//
//		/// <summary>
//		/// Security token 
//		/// </summary>
//		protected string _securityToken = string.Empty;
//
//		/// <summary>
//		/// Service url Prefix
//		/// </summary>
//		protected string _urlPrefix = string.Empty;
//		readonly HttpMessageHandler handler;
//
//		/// <summary>
//		/// Constructor
//		/// </summary>
//		/// <param name="urlPrefix">server urlPrefix</param>
//		/// <param name="securityToken">Authentication Token</param>
//		public HttpService(string urlPrefix, HttpMessageHandler aHandler = null) { //, string securityToken)
//			this.handler = aHandler;
//			if (String.IsNullOrEmpty(urlPrefix))
//				throw new ArgumentNullException("urlPrefix");
//
//			if (!urlPrefix.EndsWith("/"))
//				urlPrefix = string.Concat(urlPrefix, "/");
//
//			_urlPrefix = urlPrefix;
//			//_securityToken = securityToken.StartsWith("Bearer ") ? securityToken.Substring(7) : securityToken;
//		}
//
//		public HttpClient createClient()
//		{
//			if (!CrossConnectivity.Current.IsConnected)
//				throw new HttpRequestException("Not connected to the internet");
//			HttpClient httpClient;
//			if (handler == null)
//				httpClient = new HttpClient();
//			else
//				httpClient = new HttpClient(handler);
//				
//			httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
//			//httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _securityToken);
//			//if (JWTToken!=null && JWTToken.Length>0)
//			//	httpClient.DefaultRequestHeaders.Add("Authorization", JWTToken);
//			return httpClient;
//		}
//
//		public static string CombineUri(params string[] uriParts)
//		{
//			string uri = string.Empty;
//			if (uriParts != null && uriParts.Any())
//			{
//				char[] trims = new char[] { '\\', '/' };
//				uri = (uriParts[0] ?? string.Empty).TrimEnd(trims);
//
//				for (int i = 1; i < uriParts.Length; i++)
//				{
//					uri = string.Format("{0}/{1}", uri.TrimEnd(trims), (uriParts[i] ?? string.Empty).TrimStart(trims));
//				}
//			}
//			return uri;
//		}
//
//		public string expandUrl(string aUrl){
//			return CombineUri(_urlPrefix,aUrl);
//		}
//
//		/// <summary>
//		/// Do GetByVisitor
//		/// </summary>
//		/// <param name="url"></param>
//		/// <returns></returns>
//		//http://stackoverflow.com/questions/18192357/deserializing-json-object-array-with-json-net
//		public async Task<T> GetAsync<T>(string aUrl) where T : class {
//			var response = await Task.Run(async () => await GetAsync (aUrl));
//			response.EnsureSuccessStatusCode ();
//			var s = await response.Content.ReadAsStringAsync ();
//			return Json.Deserialize<T>(s);
//		}
//				
//
//		/// <summary>
//		/// Do GetByVisitor
//		/// </summary>
//		/// <param name="url"></param>
//		/// <returns></returns>
//		public async Task<HttpResponseMessage> GetAsync(string aUrl)
//		{
//			var httpClient = createClient();
//			var url = expandUrl(aUrl);
//			Log.Debug("Called {0} GetAsync {1} ...",this.GetType().ToString(),url);		
//			HttpResponseMessage response = null;
//			try {
//				response = await Task.Run(async () => await httpClient.GetAsync (url));
//			} catch (System.Net.WebException e) {
//				if (e.Response == null)
//					throw;
//				//response = e.Response
//				Log.Debug($"System.Net.WebException: {e.Message}");
//			}	
//
//			// !!! Strangely GetAsync throws a WebException 422 for get but not post. Need to catch it, but System.Net.WebException != HttpResponseMessage
//
//			//catch (HttpRequestException requestException)
//			//{
//			//    if (requestException.InnerException is WebException && ((WebException)requestException.InnerException).Status == WebExceptionStatus.NameResolutionFailure)
//			//    {
//			//        MessageBox.Show("chyba:\n\n" + requestException.InnerException.Message);
//			//    }
//			//}
//			
//			
//			//	if (e.Message.Contains ("Not connected to the internet"))
//			//		throw new NoNetworkException (e.Message);
//			//	else
//			//		throw;
//			Log.Debug("{0} GetAsync {1} => {2}",this.GetType().ToString(),url,response.StatusCode);
//			EnsureAuthorized (response);
//			return response;
//		}
//
//
//		/// <summary>
//		/// Do post with results
//		/// </summary>
//		/// <param name="url"></param>
//		/// <param name="entity"></param>
//		/// <returns></returns>
//		public async Task<T> PostAsync<T, U>(string aUrl, U entity) where T : class {
//			//var httpClient = createClient();
//			//var content = JsonConvert.SerializeObject(entity);
//			//var url = expandUrl(aUrl);
//			//Log.Debug("Called {0} PostAsync {1} Content: {2} ...",this.GetType().ToString(),url,content);
//			//var response = await httpClient.PostAsync(url, new StringContent(content, Encoding.UTF8, "application/json"));
//			//Log.Debug("{0} PostAsync {1} => {2}",this.GetType().ToString(),url,response.StatusCode);
//			var response = await Task.Run(async () => await PostAsync<U> (aUrl, entity));
//			string responseContent = await response.Content.ReadAsStringAsync();
//			//return JsonConvert.DeserializeObject<T>(responseContent);
//			return Json.Deserialize<T>(responseContent);//  JsonConvert.SerializeObject(entity);
//		}
//
//		/// <summary>
//		/// Do post
//		/// </summary>
//		/// <param name="url"></param>
//		/// <param name="entity"></param>
//		/// <returns></returns>
//		public async Task<HttpResponseMessage> PostAsync<T>(string aUrl, T entity,IDictionary<string,string> aHeaders = null) {
//			var httpClient = createClient();
//			//var content = JsonConvert.SerializeObject(entity);
//			var content = entity!=null ? Json.Serialize(entity) : "";//  JsonConvert.SerializeObject(entity);
//			var body = new StringContent (content, Encoding.UTF8, "application/json");
//			if (aHeaders != null) {
//				foreach (var k in aHeaders.Keys) {//  Pr  .GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
//					body.Headers.Add (k, aHeaders[k]);
//				}
//			}
//			var url = expandUrl(aUrl);
//			Log.Debug("Called {0} PostAsync {1} Content: {2} ...",this.GetType().ToString(),url,content);
//			HttpResponseMessage response;
//			try {
//				response = await Task.Run(async () => await httpClient.PostAsync (url, body));
//			} catch (Exception e) {
//				if (e.Message.Contains ("Not connected to the internet"))
//					throw new NoNetworkException (e.Message);
//				else
//					throw;
//			}
//			EnsureAuthorized (response);			
//			Log.Debug("{0} PostAsync {1} => {2}",this.GetType().ToString(),url,response.StatusCode);
//			return response;
//		}
//
//		/// <summary>
//		/// Do post
//		/// </summary>
//		/// <param name="url"></param>
//		/// <returns></returns>
//		public async Task<HttpResponseMessage> PostAsync(string aUrl) {
//			return await PostAsync<Object>(aUrl, null);
//		}
//
//		/// <summary>
//		/// Put
//		/// </summary>
//		/// <param name="url"></param>
//		/// <param name="entity"></param>
//		/// <returns></returns>
//		public async Task<HttpResponseMessage> PutAsync<T>(string aUrl, T entity)
//		{
//			var httpClient = createClient();
//			//var content = JsonConvert.SerializeObject(entity);
//			var content = Json.Serialize(entity);//  JsonConvert.SerializeObject(entity);
//			var url = expandUrl(aUrl);
//			Log.Debug("Called {0} PutAsync {1} ...",this.GetType().ToString(),url);
//			HttpResponseMessage response;
//			//try {
//			response = await Task.Run(async () => await httpClient.PutAsync (url, new StringContent (content, Encoding.UTF8, "application/json")));
//			//} catch (HttpRequestException e) {
//			//	if (e.Message.Contains ("Not connected to the internet"))
//			//		throw new NoNetworkException (e.Message);
//			//	else
//			//		throw;
//			//}
//			EnsureAuthorized (response);
//			Log.Debug("{0} PutAsync {1} => {2}",this.GetType().ToString(),url,response.StatusCode);
//			return response;
//		}
//
//		/// <summary>
//		/// Put
//		/// </summary>
//		/// <param name="url"></param>
//		/// <returns></returns>
//		public async Task<HttpResponseMessage> DeleteAsync(string aUrl)
//		{
//			var httpClient = createClient();
//			var url = expandUrl(aUrl);
//			Log.Debug("Called {0} DeleteAsync {1} ...",this.GetType().ToString(),url);
//			HttpResponseMessage response;
//			//try {
//			response = await Task.Run(async () => await httpClient.DeleteAsync (url));
//			//} catch (HttpRequestException e) {
//			//	if (e.Message.Contains ("Not connected to the internet"))
//			//		throw new NoNetworkException (e.Message);
//			//	else
//			//		throw;
//			//}
//			EnsureAuthorized (response);
//			Log.Debug("{0} DeleteAsync {1} => {2}",this.GetType().ToString(),url,response.StatusCode);
//			return response;
//		}
//
//	}
//}
//
