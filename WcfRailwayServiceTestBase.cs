// vryabchuk

using AutoMapper;
using Backend.ServiceHosting.WCF.Factory;
using Backend.ServiceHosting.WCF.Logging;
using Backend.ServiceHosting.WCF.Transport;
using DataModel;
using Log;
using Railway.Contracts;
using Railway.DataLayer;
using Railway.Implementation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Xml.Linq;
using System.Xml.XPath;
using Railway.Tests.WCF;
using Ufs.Framework.Services.Contracts.Common.CommonMapper;
using Ufs.Framework.Services.Contracts.Common.Infrastructure.SoapHeaders;
using UfsDataModel;
using LibException;

namespace Railway.Tests.Wcf
{
    /// <summary>
    /// 
    /// </summary>
    /// <author> Ryabchuk Vitalii</author>
    public class WcfRailwayServiceTestBase
    {
        protected long KbAtExecution;

        /// <summary>
        /// Базовый адрес для оконечной точки сервиса
        /// </summary>
        protected const string Basehostaddress = "http://localhost:8002/TestWcf";
        /// <summary>
        /// Относительный адрес оконечной точки сервиса для Soap
        /// </summary>
        protected const string SoapServiceAddressing = "SOAP";

        /// <summary>
        /// Относительный адрес оконечной точки сервиса для Rest
        /// </summary>
        protected const string RestServiceAddressing = "REST";

        /// <summary>
        /// Получить экземпляр сервиса
        /// </summary>
        /// <returns></returns>
        protected ServiceHost GetServiceHost()
        {

            BindingElementCollection serverBindingElementCollection = new BindingElementCollection
                                                                          {
                                                                              new BinaryMessageEncodingBindingElement()
                                                                                  {
                                                                                      ReaderQuotas =
                                                                                          {
                                                                                              MaxArrayLength = 2000000,
                                                                                              MaxStringContentLength = 2000000
                                                                                          }
                                                                                  }
                                                                          };
            var securityBindingElement = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
            securityBindingElement.AllowInsecureTransport = true;
            serverBindingElementCollection.Add(securityBindingElement);
            var ufsHttpTranspor = new UFSHttpTransportBindingElement() { MaxBufferSize = 9999999, MaxReceivedMessageSize = 20000000 };
            serverBindingElementCollection.Add(ufsHttpTranspor);
            CustomBinding serverBinding = new CustomBinding(serverBindingElementCollection);
            serverBinding.ReceiveTimeout = new TimeSpan(0, 0, 10);
            serverBinding.SendTimeout = new TimeSpan(0, 0, 10);
            var serviceHostFactory = new SoapServiceHostFactory();
            ServiceHost serviceHost = serviceHostFactory.CreateSoapServiceHost(typeof(RailwaySoapService), new[] { new Uri(Basehostaddress) });
            serviceHost.AddServiceEndpoint(typeof(IRailwaySoap), serverBinding, SoapServiceAddressing);
            Mapper.Initialize(cfg => cfg.AddProfile<CommonMappingProfile>());
            var behavior = new ServiceMetadataBehavior { HttpGetEnabled = true };
            serviceHost.Description.Behaviors.Add(behavior);
            serviceHost.AddServiceEndpoint(typeof(IMetadataExchange), MetadataExchangeBindings.CreateMexHttpBinding(), "mex");
            return serviceHost;
        }

        /// <summary>
        /// Получить экземпляр сервиса
        /// </summary>
        /// <returns></returns>
        protected ServiceHost GetRestServiceHost()
        {
            BindingElementCollection serverBindingElementCollection = new BindingElementCollection
                                                                          {
                                                                              new BinaryMessageEncodingBindingElement()
                                                                          };

            var serviceHostFactory = new Railway.ServiceHosting.WcfRest.Factory.RestServiceHostFactory();
            ServiceHost serviceHost = serviceHostFactory.CreateServiceHostTest(typeof(RailwayRestService), new[] { new Uri(Basehostaddress) });
            WebHttpBinding serverRestBiding = new WebHttpBinding(WebHttpSecurityMode.None);   
            var serviceEndpoint = serviceHost.AddServiceEndpoint(typeof(IRailwayRest), serverRestBiding, RestServiceAddressing);
            Mapper.Initialize(cfg => cfg.AddProfile<CommonMappingProfile>());
            serviceEndpoint.Behaviors.Add(new WebHttpBehavior());             
            foreach (var op in serviceEndpoint.Contract.Operations)
            {               
                UrlDecodeFormatterBehavior.ReplaceFormatterBehavior(op, serviceEndpoint.Address);
            }
            //serviceEndpoint.Behaviors.Insert(0, new Railway.ServiceHosting.WcfRest.Factory.LoggingBehavior(() => new NullLogger()));              
            var behavior = new ServiceMetadataBehavior { HttpGetEnabled = true };
            serviceHost.Description.Behaviors.Add(behavior);
            return serviceHost;
        }

        /// <summary>
        /// Получить экземпляр слиента
        /// </summary>
        /// <returns></returns>
        protected IRailwaySoap GetClientChannel(string userName ="testMerchant"/*"ufs"*/, string password = "testMerchant1"/*"ufs123"*/,
            string terminal = null, string advert = null, string customerId = null, string clientIpAddressChain = null)
        {
            var securityBindingElement = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
            securityBindingElement.AllowInsecureTransport = true;
            var bme = new BinaryMessageEncodingBindingElement
            {
                ReaderQuotas =
                {
                    MaxArrayLength = 2000000,
                    MaxStringContentLength = 2000000
                }
            };
            var clientBindingElementCollection = new BindingElementCollection
                                                     {
                                                         bme,
                                                         securityBindingElement
                                                     };
            var htbe = new HttpTransportBindingElement { MaxBufferSize = 20000000, MaxReceivedMessageSize = 20000000 };
            clientBindingElementCollection.Add(htbe);
            var clientBinding = new CustomBinding(clientBindingElementCollection);
            clientBinding.ReceiveTimeout = new TimeSpan(0, 10, 0);
            clientBinding.SendTimeout = new TimeSpan(0, 10, 0);
            var factory = new ChannelFactory<IRailwaySoap>(clientBinding, Basehostaddress + "/" + SoapServiceAddressing);
            factory.Endpoint.Behaviors.Add(new SoapEndpointBehavior(new NextLogger("SOAP_RailwayClient")));
            foreach (var op in factory.Endpoint.Contract.Operations)
            {
                op.Behaviors.Add(new CredentialsOperationBehavior(terminal, advert, customerId, clientIpAddressChain));
            }
            factory.Credentials.UserName.UserName = userName;
            factory.Credentials.UserName.Password = password;
            return factory.CreateChannel();
        }


        /// <summary>
        /// Получить экземпляр слиента
        /// </summary>
        /// <returns></returns>
        protected IRailwaySoapAsync GetClientAsyncChannel(string userName = "testMerchant"/*"ufs"*/, string password = "testMerchant1"/*"ufs123"*/,
            string terminal = null, string advert = null, string customerId = null, string clientIpAddressChain = null)
        {
            var securityBindingElement = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
            securityBindingElement.AllowInsecureTransport = true;
            var bme = new BinaryMessageEncodingBindingElement
            {
                ReaderQuotas =
                {
                    MaxArrayLength = 2000000,
                    MaxStringContentLength = 2000000
                }
            };
            var clientBindingElementCollection = new BindingElementCollection
                                                     {
                                                         bme,
                                                         securityBindingElement
                                                     };
            var htbe = new HttpTransportBindingElement { MaxBufferSize = 20000000, MaxReceivedMessageSize = 20000000 };
            clientBindingElementCollection.Add(htbe);
            var clientBinding = new CustomBinding(clientBindingElementCollection);
            clientBinding.ReceiveTimeout = new TimeSpan(0, 10, 0);
            clientBinding.SendTimeout = new TimeSpan(0, 10, 0);
            var factory = new ChannelFactory<IRailwaySoapAsync>(clientBinding, Basehostaddress + "/" + SoapServiceAddressing);
            factory.Endpoint.Behaviors.Add(new SoapEndpointBehavior(new NextLogger("SOAP_RailwayClient")));
            foreach (var op in factory.Endpoint.Contract.Operations)
            {
                op.Behaviors.Add(new CredentialsOperationBehavior(terminal, advert, customerId, clientIpAddressChain));
            }
            factory.Credentials.UserName.UserName = userName;
            factory.Credentials.UserName.Password = password;
            return factory.CreateChannel();
        }

        /// <summary>
        /// Получить экземпляр слиента
        /// </summary>
        /// <returns></returns>
        protected IRailwayRest GetRESTClientChannel(string userName = "testMerchant", string password = "testMerchant1", string terminal = null, string advert = null, string clientIpAddressChain = null)
        {
            return new RestClient(Basehostaddress + "/" + RestServiceAddressing, userName, password, terminal, advert, clientIpAddressChain);
        }

        /// <summary>
        /// Обнуления счетчика памяти
        /// </summary>
        protected void ResetMemoryCounter()
        {
            KbAtExecution = GC.GetTotalMemory(false) / 1024;
        }

        /// <summary>
        /// Вывод информации об использовании пакмяти
        /// </summary>
        /// <param name="method"></param>
        protected void ShowMemoryCounter(string method)
        {
            var kbAfter1 = GC.GetTotalMemory(false) / 1024;
            var kbAfter2 = GC.GetTotalMemory(true) / 1024;
            System.Diagnostics.Debug.WriteLine("*\n*\n*\n");
            System.Diagnostics.Debug.WriteLine("*****   " + method + "   *****");
            System.Diagnostics.Debug.WriteLine(KbAtExecution + " Started with this kb.");
            System.Diagnostics.Debug.WriteLine(kbAfter1 + " After the test.");
            System.Diagnostics.Debug.WriteLine(kbAfter1 - KbAtExecution + " Amt. Added.");
            System.Diagnostics.Debug.WriteLine(kbAfter2 + " Amt. After Collection");
            System.Diagnostics.Debug.WriteLine(kbAfter2 - kbAfter1 + " Amt. Collected by GC.");
            System.Diagnostics.Debug.WriteLine("*\n*\n*\n");
            System.Diagnostics.Debug.WriteLine("*****    " + method + "   *****");
            System.Diagnostics.Debug.WriteLine("End");
        }


        protected static RailwayDataLayer GetDatalayer()
        {
            ILog log = new NextLogger("Railway");
            IUnitOfWork unitOfWork = UnitFactory.DbUnitofWork();
            return new RailwayDataLayer(log, unitOfWork);
        }

        protected Trans GetTrans(int idtrans)
        {
            return GetDatalayer().GetTransaction(idtrans);
        }
    }

    public static class StreamExtension
    {
        public static XElement ToXElement(this Stream stream)
        {
            stream.Position = 0;
            var content = new StreamReader(stream).ReadToEnd();
            return XDocument.Parse(content).Root;
        }

        public static IEnumerable<XElement> Descendants(this Stream stream, string input)
        {
            return ToXElement(stream).Descendants(input);
        }

        public static IEnumerable<XElement> XPathSelectElements(this Stream stream, string input)
        {
            return ToXElement(stream).XPathSelectElements(input);
        }

    }

    /// <summary>
    /// REST клиент для Railway
    /// </summary>
    /// <author> Ryabchuk Vitalii</author>
    internal class RestClient : IRailwayRest
    {
        private readonly string userName;
        private readonly string password;
        private readonly string terminal;
        private readonly string advert;
        private readonly Uri baseAddress;
        private readonly Encoding RequestEncoding;
        private readonly string clientIpAddressChain;

        internal RestClient(string baseUrl, string userName = "ufs", string password = "ufs123", string terminal = null,
            string advert = null, string clientIpAddressChain = null)
        {
            this.userName = userName;
            this.password = password;
            this.terminal = terminal;
            this.advert = advert;
            this.clientIpAddressChain = clientIpAddressChain;
            baseAddress = new Uri(baseUrl);
            GlobalizationSection configSection =
                (GlobalizationSection) WebConfigurationManager.GetSection("system.web/globalization");
            RequestEncoding = configSection.RequestEncoding;
        }



        /// <summary>
        /// url encoding 1251
        /// </summary>
        public Uri UrlEncode(string uriTemplate, Dictionary<string, string> dic)
        {
            dic.Add(TerminalHeader.Name, terminal);
            dic.Add(AdvertHeader.NameOld, advert);
            dic.Add(ClientIpAddressChainHeader.Name, clientIpAddressChain);
            uriTemplate = uriTemplate + "&" + AdvertHeader.NameOld + "={" + AdvertHeader.NameOld + "}&" +
                          TerminalHeader.Name + "={" + TerminalHeader.Name + "}&" +
                          ClientIpAddressChainHeader.Name + "={" + ClientIpAddressChainHeader.Name + "}";
            foreach (string key in dic.Keys)
            {
                uriTemplate = uriTemplate.Replace("{" + key + "}", HttpUtility.UrlEncode(dic[key], RequestEncoding));
            }

            Regex rgx = new Regex(@"{\w+}");
            uriTemplate = rgx.Replace(uriTemplate, "");

            return new Uri(baseAddress.AbsoluteUri + "/" + uriTemplate);
        }


        private void SetCredential(HttpRequestMessage req)
        {

            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            //string par = "";
            //if (!string.IsNullOrEmpty(terminal))
            //{
            //    par += String.Format("{0}={1}", TerminalHeader.Name, terminal);
            //}
            //if (!string.IsNullOrEmpty(advert))
            //{
            //    par += String.Format("&{0}={1}", AdvertHeader.Name, advert);
            //}
            //req.Headers.Add(GenericHeader.Name, par);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(string.Format("{0}:{1}", userName, password))));
        }

        /// <summary>
        /// Получение расписания движения поездов с возможностью дальнейшей покупки билетов.
        /// </summary>
        /// <param name="lang">Язык запроса </param>
        /// <param name="from">Наименование или код станции отправления</param>
        /// <param name="to">Наименование или код станции прибытия</param>
        /// <param name="month">Месяц отправления</param>
        /// <param name="day">День отправления</param>
        /// <param name="time_from">Левая граница временного диапазона (отправления или прибытия)</param>
        /// <param name="time_to">Правая граница временного диапазона (отправления или прибытия)</param>
        /// <param name="time_sw">Влияет на применение ограничивающих параметров Time_from и Time_to </param>
        /// <param name="train_with_seat">Признак отображения поездов без свободных мест. Если параметр не передан или передано значение Train_with_seat=1, то в выдачи вернутся только поезда со свободными местами.</param>
        /// <param name="grouppingType">Признак группировки поездов. Если значение не передано, то группировка поездов осуществляется по типу вагона </param>
        /// <param name="childPrice">Признак запроса информации по стоимости детских мест. </param>
        /// <param name="joinTrains">Признак склейки поездов в один логический</param>
        /// <param name="testCaseId"></param>
        /// <param name="lpToken"> Токен сессии пользователя в АСУМД. Используется для премиальных билетов </param>
        /// <param name="sourceType">Тип источника</param>
        /// <returns></returns>
        public XElement TrainList(string lang, string from, string to, string month, string day, string time_sw,
            string time_from, string time_to, string train_with_seat, string grouppingType, string childPrice,
            string joinTrains, string testCaseId,
            string joinTrainComplex = "0",
            string nearDaysCount = "3", string maxSegmentCount = "2", string minHoursBetweenSegments = "1",
            string maxHoursBetweenSegments = "12", string lpToken = null, string sourceType = null,
            string carrier = null, string searchOption = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "TrainList?lang={lang}&from={from}&to={to}&month={month}&day={day}&time_from={time_from}&time_to={time_to}&time_sw={time_sw}&train_with_seat={train_with_seat}&grouppingType={grouppingType}&childPrice={childPrice}&join_Trains={join_Trains}&testCaseId={testCaseId}&joinTrainComplex={joinTrainComplex}&lpToken={lpToken}&isMmp={isMmp}&sourceType={sourceType}&carrier={carrier}&searchOption={searchOption}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"from", from},
                    {"to", to},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"time_from", time_from},
                    {"time_to", time_to},
                    {"time_sw", time_sw},
                    {"train_with_seat", train_with_seat},
                    {"grouppingType", grouppingType},
                    {"childPrice", childPrice},
                    {"join_Trains", joinTrains},
                    {"testCaseId", testCaseId},
                    {"joinTrainComplex", joinTrainComplex},
                    {"nearDaysCount", nearDaysCount},
                    {"maxSegmentCount", maxSegmentCount},
                    {"minHoursBetweenSegments", minHoursBetweenSegments},
                    {"maxHoursBetweenSegments", maxHoursBetweenSegments},
                    {"lpToken", lpToken},
                    {"sourceType", sourceType},
                    {"carrier", carrier},
                    {"searchOption", searchOption}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }


        /// <summary>
        /// Получение информации о маршруте следования поезда 
        /// </summary> 
        /// <param name="lang">Язык запроса</param>
        /// <param name="train">Поезд</param>
        /// <param name="from">Наименование или код станции отправления пассажира</param>
        /// <param name="month">Месяц </param>
        /// <param name="day">День</param>
        /// <param name="useStaticSchedule">Обращаться в статическую базу полугодового расписания</param>
        /// <param name="testCaseId"></param>
        /// <param name="suburban">Электрички</param>
        /// <param name="to">Наименование или код станции прибытия пассажира</param>
        /// <returns></returns>
        public XElement TrainRoute(string lang, string train, string from, string month, string day,
            string useStaticSchedule, string testCaseId, string suburban = null, string to = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 2, 0);
                var uriTemplate =
                    "TrainRoute?lang={lang}&from={from}&train={train}&month={month}&day={day}&useStaticSchedule={useStaticSchedule}&testCaseId={testCaseId}&suburban={suburban}&to={to}";
                var dic = new Dictionary<string, string>()
                {
                    {"from", from},
                    {"train", train},
                    {"lang", lang},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"useStaticSchedule", useStaticSchedule},
                    {"testCaseId", testCaseId},
                    {"suburban", suburban},
                    {"to", to}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Получение информации о маршруте следования поезда 
        /// </summary> 
        /// <param name="lang">Язык запроса</param>
        /// <param name="from">Наименование или код станции отправления пассажира</param>
        /// <param name="month">Месяц </param>
        /// <param name="day">День</param>
        /// <param name="useStaticSchedule">Обращаться в статическую базу полугодового расписания</param>
        /// <param name="suburban">Электрички</param>
        /// <returns></returns>
        public XElement StationRoute(string lang, string from, string month, string day, string useStaticSchedule,
            string suburban = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 2, 0);
                var uriTemplate =
                    "StationRoute?lang={lang}&from={from}&month={month}&day={day}&useStaticSchedule={useStaticSchedule}&suburban={suburban}";
                var dic = new Dictionary<string, string>()
                {
                    {"lang", lang},
                    {"from", from},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"useStaticSchedule", useStaticSchedule},
                    {"suburban", suburban}
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Получение информации о движении поездов, наличии свободных мест и стоимости проезда.
        /// </summary>
        /// <param name="lang">Язык запроса</param>
        /// <param name="from">Наименование или код станции отправления пассажира</param>
        /// <param name="to">Наименование или код станции прибытия пассажира </param>
        /// <param name="month">Месяц </param>
        /// <param name="day">День</param>
        /// <param name="time_sw">Влияет на применение ограничивающих параметров Time_from и Time_to </param>
        /// <param name="time_from">Левая граница временного диапазона (отправления или прибытия) </param>
        /// <param name="time_to">Правая граница временного диапазона (отправления или прибытия)</param>
        /// <param name="childPrice">Показывать ли цены для детского тарифа </param>
        /// <param name="suburban">Электрички</param>
        /// <param name="showDepartedTrains">Показать уже ушедшие поезда</param>
        /// <param name="sourceType">Тип источника</param>
        /// <returns>XML</returns>
        public XElement TimeTable(string from, string to, string day, string month, string lang, string time_sw,
            string time_from, string time_to, string childPrice = "", string testCaseId = null, string suburban = null,
            string showDepartedTrains = null, string sourceType = null, string showPrices = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);

                var uriTemplate =
                    "TimeTable?from={from}&to={to}&day={day}&month={month}&lang={lang}&time_sw={time_sw}&time_from={time_from}&time_to={time_to}&childPrice={childPrice}&testCaseId={testCaseId}&suburban={suburban}&showDepartedTrains={showDepartedTrains}&sourceType={sourceType}&carrier={carrier}&showPrices={showPrices}";
                var dic = new Dictionary<string, string>
                {
                    {"from", from},
                    {"to", to},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"lang", lang},
                    {"time_sw", time_sw},
                    {"time_from", time_from},
                    {"time_to", time_to},
                    {"childPrice", childPrice},
                    {"testCaseId", testCaseId},
                    {"showDepartedTrains", showDepartedTrains},
                    {"suburban", suburban},
                    {"showPrices", showPrices }
                };

                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }


        public XElement CarListEx(string lang, string from, string to, string month, string day, string train,
            string grouppingType = "", string typecar = "", string testCaseId = null, string tariffType = "",
            string serviceclass = "", string time = "", string lpToken = "", string sourceType = "", string carrier = "")
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "CarListEx?lang={lang}&train={train}&from={from}&to={to}&month={month}&day={day}&GrouppingType={grouppingType}&type_car={typecar}&testCaseId={testCaseId}&tariffType={tariffType}&serviceclass={serviceclass}&time={time}&lpToken={lpToken}&carrier={carrier}";
                var dic = new Dictionary<string, string>()
                {
                    {"lang", lang},
                    {"from", from},
                    {"train", train},
                    {"to", to},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"grouppingType", grouppingType},
                    {"testCaseId", testCaseId},
                    {"typecar", typecar},
                    {"tariffType", tariffType},
                    {"serviceclass", serviceclass},
                    {"time", time},
                    {"lpToken", lpToken},
                    {"carrier", carrier}
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="day"></param>
        /// <param name="month"></param>
        /// <param name="train"></param>
        /// <param name="ncar"></param>
        /// <param name="typecar"></param>
        /// <param name="serviceclass"></param>
        /// <param name="adultdoc"></param>
        /// <param name="childdoc"></param>
        /// <param name="babydoc"></param>
        /// <param name="sex"></param>
        /// <param name="diapason"></param>
        /// <param name="nup"></param>
        /// <param name="ndown"></param>
        /// <param name="inonekupe"></param>
        /// <param name="bedding"></param>
        /// <param name="stan"></param>
        /// <param name="remoteCheckIn"></param>
        /// <param name="gdterm"></param>
        /// <param name="placedemands"></param>
        /// <param name="email"></param>
        /// <param name="idcust"></param>
        /// <param name="paytype"></param>
        /// <param name="time"></param>
        /// <param name="comment"></param>
        /// <param name="phone"></param>
        /// <param name="advertDomain"></param>
        /// <param name="creditcard"></param>
        /// <param name="internationalServiceClass"></param>
        /// <param name="lpToken"> Токен сессии пользователя в АСУМД. Используется для премиальных билетов </param>
        /// <param name="lpCardNumber"> Номер премиальной карты. Используется для премиальных билетов  </param>
        /// <param name="full_kupe"></param>
        /// <param name="deviceId">ID устройства</param>
        /// <returns></returns>
        public XElement BuyTicket(
            string @from = null,
            string to = null,
            string day = null,
            string month = null,
            string train = null,
            string typecar = null,
            string adultdoc = null,
            string childdoc = null,
            string babydoc = null,
            string ncar = null,
            string serviceclass = null,
            string sex = null,
            string diapason = null,
            string nup = null,
            string ndown = null,
            string inonekupe = null,
            string bedding = null,
            string stan = null,
            string remoteCheckIn = null,
            string gdterm = null,
            string placedemands = null,
            string email = null,
            string idcust = null,
            string paytype = null,
            string time = null,
            string comment = null,
            string phone = null,
            string lang = null,
            string testCaseId = null,
            string creditcard = null,
            string internationalServiceClass = null,
            string passdoc = null,
            string lpToken = null,
            string lpCardNumber = null,
            string full_kupe = null,
            string deviceId = null
            )
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "BuyTicket?from={from}&to={to}&day={day}&month={month}&train={train}&type_car={typecar}&adult_doc={adultdoc}&child_doc={childdoc}&baby_doc={babydoc}&n_car={ncar}&service_class={serviceclass}&sex={sex}&diapason={diapason}&n_up={nup}&n_down={ndown}&in_one_kupe={inonekupe}&bedding={bedding}&STAN={stan}&remoteCheckIn={remoteCheckIn}&gdterm={gdterm}&placedemands={placedemands}&email={email}&idcust={idcust}&paytype={paytype}&time={time}&comment={comment}&phone={phone}&lang={lang}&testCaseId={testCaseId}&creditcard={creditcard}&internationalServiceClass={internationalServiceClass}&pass_doc={passdoc}&lpToken={lpToken}&lpCardNumber={lpCardNumber}&full_kupe={full_kupe}&deviceId={deviceId}";

                var dic = new Dictionary<string, string>()
                {
                    {"from", from},
                    {"to", to},
                    {"day", day != null ? day.ToString(CultureInfo.InvariantCulture) : null},
                    {"month", month != null ? month.ToString(CultureInfo.InvariantCulture) : null},
                    {"train", train},
                    {"typecar", typecar},
                    {"adultdoc", adultdoc},
                    {"childdoc", childdoc},
                    {"babydoc", babydoc},
                    {"ncar", ncar},
                    {"serviceclass", serviceclass},
                    {"sex", sex},
                    {"diapason", diapason},
                    {"nup", nup},
                    {"ndown", ndown},
                    {"inonekupe", inonekupe},
                    {"bedding", bedding},
                    {"stan", stan},
                    {"remoteCheckIn", remoteCheckIn},
                    {"gdterm", gdterm},
                    {"placedemands", placedemands},
                    {"email", email},
                    {"idcust", idcust},
                    {"paytype", paytype},
                    {"time", time},
                    {"comment", comment},
                    {"phone", phone},
                    {"lang", lang},
                    {"testCaseId", testCaseId},
                    {"creditcard", creditcard},
                    {"internationalServiceClass", internationalServiceClass},
                    {"passdoc", passdoc},
                    {"lpToken", lpToken},
                    {"lpCardNumber", lpCardNumber},
                    {"full_kupe", full_kupe},
                    {"deviceId", deviceId}
                };

                dic = dic.Where(x => x.Value != null).ToDictionary(x => x.Key, y => y.Value);
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement TransRefunds(string idtrans)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "TransRefunds?idtrans={idtrans}";
                var dic = new Dictionary<string, string>
                {
                    {"idtrans", idtrans},
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        #region POST

        public XElement BuyTicketPostXml(string inputXml)
        {
            return ExecutePost(inputXml, "BuyTicketPostXml?");
        }

        public XElement BuyTicketXml(string inputXml)
        {
            return ExecutePost(inputXml, "BuyTicketXml?");
        }

        public XElement TrainListPost(string inputXml)
        {
            return ExecutePost(inputXml, "TrainList?");
        }

        public XElement TrainRoutePost(string inputXml)
        {
            return ExecutePost(inputXml, "TrainRoute?");
        }

        public XElement StationRoutePost(string inputXml)
        {
            return ExecutePost(inputXml, "StationRoute?");
        }

        public XElement TimeTablePost(string inputXml)
        {
            return ExecutePost(inputXml, "TimeTable?");
        }

        public XElement CarListExPost(string inputXml)
        {
            return ExecutePost(inputXml, "CarListEx?");
        }

        public Stream GetTicketBlankPost(string inputXml)
        {
            return ExecutePostStream(inputXml, "GetTicketBlank?");
        }

        public XElement BuyTicketPost(string inputXml)
        {
            return ExecutePost(inputXml, "BuyTicket?");
        }

        public XElement TrainListComplexPost(string inputXml)
        {
            return ExecutePost(inputXml, "TrainListComplex?");
        }

        public XElement RefundAmountPost(string inputXml)
        {
            return ExecutePost(inputXml, "RefundAmount?");
        }

        public XElement RefundPost(string inputXml)
        {
            return ExecutePost(inputXml, "Refund?");
        }

        public XElement ConfirmTicketPost(string inputXml)
        {
            return ExecutePost(inputXml, "ConfirmTicket?");
        }

        public XElement UpdateOrderInfoPost(string inputXml)
        {
            return ExecutePost(inputXml, "UpdateOrderInfo?");
        }

        public XElement ElectronicRegistrationPost(string inputXml)
        {
            return ExecutePost(inputXml, "ElectronicRegistration?");
        }

        public XElement TransInfoPost(string inputXml)
        {
            return ExecutePost(inputXml, "TransInfo?");
        }

        public XElement TransListPost(string inputXml)
        {
            return ExecutePost(inputXml, "TransList?");
        }

        public XElement GetCatalogPost(string inputXml)
        {
            return ExecutePost(inputXml, "GetCatalog?");
        }

        public Stream PrintOrderPost(string inputXml)
        {
            return ExecutePostStream(inputXml, "PrintOrder?");
        }

        public XElement LoyaltyServiceReversePost(string inputXml)
        {
            return ExecutePost(inputXml, "LoyaltyServiceReverse?");
        }

        public XElement LoyaltyServiceGetBalancePost(string inputXml)
        {
            return ExecutePost(inputXml, "LoyaltyServiceGetBalance?");
        }

        public XElement LoyaltyServiceLogoutPost(string inputXml)
        {
            return ExecutePost(inputXml, "LoyaltyServiceLogout?");
        }

        public XElement LoyaltyServiceAuthorizePost(string inputXml)
        {
            return ExecutePost(inputXml, "LoyaltyServiceAuthorize?");
        }

        public XElement ChangeFoodPost(string inputXml)
        {
            return ExecutePost(inputXml, "ChangeFood?");
        }

        public XElement AvailableFoodPost(string inputXml)
        {
            return ExecutePost(inputXml, "AvailableFood?");
        }

        public XElement StationInfoPost(string inputXml)
        {
            return ExecutePost(inputXml, "StationInfo?");
        }

        public XElement StationInfoListPost(string inputXml)
        {
            return ExecutePost(inputXml, "StationInfoList?");
        }

        public XElement TicketInfoPost(string inputXml)
        {
            return ExecutePost(inputXml, "TicketInfo?");
        }

        public XElement TransRefundsPost(string inputXml)
        {
            return ExecutePost(inputXml, "TransRefunds?");
        }

        public XElement GetTariffsInfoPost()
        {
            return ExecutePost(null, "GetTariffsInfo?");
        }

        private Stream ExecutePostStream(string inputXml, string uriTemplate)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var dic = new Dictionary<string, string>();
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    Content = new StringContent(inputXml),
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                var content = client.SendAsync(rm).Result.Content.ReadAsStreamAsync().Result;
                return content;
            }
        }

        private XElement ExecutePost(string inputXml, string uriTemplate)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var dic = new Dictionary<string, string>();
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = UrlEncode(uriTemplate, dic),
                };
                if (!String.IsNullOrEmpty(inputXml))
                {
                    rm.Content = new StringContent(HttpUtility.UrlEncode(inputXml));
                }
                SetCredential(rm);
                var result = client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result;
                return String.IsNullOrEmpty(result) ? null : XElement.Parse(result);
            }
        }

        #endregion

        /// <summary>
        /// выполнить запрос к методу Refund сервиса(REST)
        /// </summary>
        public XElement Refund(
            string idTrans = null,
            string idBlank = null,
            string companyName = null,
            string stan = null,
            string doc = null,
            string testCaseId = null)
        {
            return Refund(idTrans, idBlank, companyName, stan, doc, false, testCaseId);
        }

        public XElement RefundAmount(
            string idTrans = null,
            string idBlank = null,
            string companyName = null,
            string stan = null,
            string doc = null,
            string testCaseId = null)
        {
            return Refund(idTrans, idBlank, companyName, stan, doc, true, testCaseId);
        }

        public XElement GetCatalog(string code, string allLanguages, string lang = null, string isDescription = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "GetCatalog?code={code}&allLanguages={allLanguages}&lang={lang}&isDescription={isDescription}";
                var dic = new Dictionary<string, string>()
                {
                    {"code", code},
                    {"allLanguages", allLanguages},
                    {"lang", lang},
                    {"isDescription", isDescription},
                };
                dic = dic.Where(x => x.Value != null).ToDictionary(x => x.Key, y => y.Value);
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }


        private XElement Refund(string idTrans, string idBlank, string companyName, string stan, string doc,
            bool refundAmount, string testCaseId = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = (refundAmount ? "RefundAmount" : "Refund") +
                                  "?idTrans={idTrans}&idBlank={idBlank}&companyName={companyName}&stan={stan}&doc={doc}&testCaseId={testCaseId}";
                var dic = new Dictionary<string, string>()
                {
                    {"idTrans", idTrans},
                    {"idBlank", idBlank},
                    {"companyName", companyName},
                    {"stan", stan},
                    {"doc", doc},
                    {"testCaseId", testCaseId},
                };
                dic = dic.Where(x => x.Value != null).ToDictionary(x => x.Key, y => y.Value);
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement TrainListComplex(string lang, string from, string to, string day, string month,
            string nearDaysCount, string maxSegmentCount, string minHoursBetweenSegments, string maxHoursBetweenSegments,
            string testCaseId = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "TrainListComplex?lang={lang}&from={from}&to={to}&month={month}&day={day}&nearDaysCount={nearDaysCount}&maxSegmentCount={maxSegmentCount}&minHoursBetweenSegments={minHoursBetweenSegments}&maxHoursBetweenSegments={maxHoursBetweenSegments}&testCaseId={testCaseId}";
                var dic = new Dictionary<string, string>()
                {
                    {"lang", lang},
                    {"from", from},
                    {"to", to},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"nearDaysCount", nearDaysCount},
                    {"maxSegmentCount", maxSegmentCount},
                    {"minHoursBetweenSegments", minHoursBetweenSegments},
                    {"maxHoursBetweenSegments", maxHoursBetweenSegments},
                    {"testCaseId", testCaseId},
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement ConfirmTicket(string idtrans = null, string confirm = null, string sitefee = null,
            string withoutsms2 = null, string testCaseId = null, string oneDayBooking = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "ConfirmTicket?idtrans={idtrans}&confirm={confirm}&site_fee={sitefee}&withoutsms2={withoutsms2}&testCaseId={testCaseId}&oneDayBooking={oneDayBooking}";
                var dic = new Dictionary<string, string>()
                {
                    {"idtrans", idtrans},
                    {"confirm", confirm},
                    {"sitefee", sitefee},
                    {"withoutsms2", withoutsms2},
                    {"testCaseId", testCaseId},
                    {"oneDayBooking", oneDayBooking},
                };
                dic = dic.Where(x => x.Value != null).ToDictionary(x => x.Key, y => y.Value);
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement UpdateOrderInfo(string idtrans, string testCaseId = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "UpdateOrderInfo?idtrans={idtrans}&testCaseId={testCaseId}";
                var dic = new Dictionary<string, string>
                {
                    {"idtrans", idtrans},
                    {"testCaseId", testCaseId}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement TransInfo(string lang, string idtrans, string type, string stan, string nbron,
            string includeSubmembers = null, string idorder = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "TransInfo?lang={lang}&idtrans={idtrans}&type={type}&stan={stan}&nbron={nbron}&includeSubmembers={includeSubmembers}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"idtrans", idtrans},
                    {"type", type},
                    {"stan", stan},
                    {"nbron", nbron},
                    {"includeSubmembers", includeSubmembers},
                    {"idorder", idorder}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement TicketInfo(string lang, string nbron)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "TicketInfo?lang={lang}&nbron={nbron}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"nbron", nbron}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        [OperationContract]
        public XElement ElectronicRegistration(string lang, string idtrans, string idblank, string reg,
            string testCaseId = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "ElectronicRegistration?lang={lang}&idtrans={idtrans}&idblank={idblank}&reg={reg}&testCaseId={testCaseId}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"idtrans", idtrans},
                    {"idblank", idblank},
                    {"reg", reg},
                    {"testCaseId", testCaseId},
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement TransList(string lang, string date, string type, string isCreditCard,
            string isReturnedOnRailwayTerminal, string testCaseId = null, string deviceId = null, string dateFrom = null,
            string dateTo = null, string includeSubmembers = "false")
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "TransList?lang={lang}&date={date}&type={type}&isCreditCard={isCreditCard}&isReturnedOnRailwayTerminal={isReturnedOnRailwayTerminal}&testCaseId={testCaseId}&deviceId={deviceId}&dateFrom={dateFrom}&dateTo={dateTo}&includeSubmembers={includeSubmembers}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"date", date},
                    {"type", type},
                    {"isCreditCard", isCreditCard},
                    {"isReturnedOnRailwayTerminal", isReturnedOnRailwayTerminal},
                    {"testCaseId", testCaseId},
                    {"deviceId", deviceId},
                    {"dateFrom", dateFrom},
                    {"dateTo", dateTo},
                    {"includeSubmembers", includeSubmembers}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public Stream PrintOrder(string forceNewTech = null, string format = null, string idtrans = null,
            string nbron = null, string orderid = null, string blankid = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "PrintOrder?forceNewTech={forceNewTech}&format={format}&idtrans={idtrans}&nbron={nbron}&orderid={orderid}&blankid={blankid}";
                var dic = new Dictionary<string, string>
                {
                    {"forceNewTech", forceNewTech},
                    {"format", format},
                    {"idtrans", idtrans},
                    {"nbron", nbron},
                    {"orderid", orderid},
                    {"blankid", blankid}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                var content = client.SendAsync(rm).Result.Content.ReadAsStreamAsync().Result;
                return content;
            }
        }

        public Stream GetWalletSecure(string format = null, string message = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "GetWalletSecure?format={format}&message={message}";
                var dic = new Dictionary<string, string>
                {
                    {"format", format},
                    {"message", message},
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                var content = client.SendAsync(rm).Result.Content.ReadAsStreamAsync().Result;
                return content;
            }
        }

        public Stream GetTicketBlank(string forceNewTech = null, string format = null, string idtrans = null,
            string nbron = null, string orderid = null, string blankid = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "GetTicketBlank?forceNewTech={forceNewTech}&format={format}&idtrans={idtrans}&nbron={nbron}&orderid={orderid}&blankid={blankid}";
                var dic = new Dictionary<string, string>
                {
                    {"forceNewTech", forceNewTech},
                    {"format", format},
                    {"idtrans", idtrans},
                    {"nbron", nbron},
                    {"orderid", orderid},
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return client.SendAsync(rm).Result.Content.ReadAsStreamAsync().Result;
            }
        }

        public Stream StationInfoList(string lang)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "StationInfoList?lang={lang}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return client.SendAsync(rm).Result.Content.ReadAsStreamAsync().Result;
            }
        }

        public Stream StationInfo(string code, string lang)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "StationInfo?lang={lang}&stationcode={stationcode}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"stationcode", code}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return client.SendAsync(rm).Result.Content.ReadAsStreamAsync().Result;
            }
        }

        public void SetEmulationInfoPost(string input)
        {
            var result = ExecutePost(input, "SetEmulationInfo?");

            if (result != null)
            {
                var error = new RZhDGateError(result);
                throw new ZException(new ZError(error.Code, error.Descr));
            }
        }

        public void ResetEmulationInfo()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "ResetEmulationInfo?";

                var dic = new Dictionary<string, string>();

                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                var result = client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result;

                if (!string.IsNullOrEmpty(result))
                {
                    var error = new RZhDGateError(XElement.Parse(result));
                    throw new ZException(new ZError(error.Code, error.Descr));
                }

            }
        }

        public XElement AvailableFood(string lang, string idtrans)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "AvailableFood?lang={lang}&idtrans={idtrans}";
                var dic = new Dictionary<string, string>()
                {
                    {"lang", lang},
                    {"idtrans", idtrans},
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement ChangeFood(string lang, string foodAllowanceCode, string idtrans = null, string blanksID = null,
            string testCaseId = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "ChangeFood?lang={lang}&idtrans={idtrans}&blanksID={blanksID}&foodAllowanceCode={foodAllowanceCode}&testCaseId={testCaseId}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"idtrans", idtrans},
                    {"blanksID", blanksID},
                    {"foodAllowanceCode", foodAllowanceCode},
                    {"testCaseId", testCaseId},
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        public XElement GetTariffsInfo()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode("GetTariffsInfo?", new Dictionary<string, string>())
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Получение станций пересадки
        /// </summary>
        /// <param name="lang">Язык</param>
        /// <param name="from">Откуда</param>
        /// <param name="to">Куда</param>
        /// <param name="day">День меняца</param>
        /// <param name="month">Месяц года</param>
        /// <param name="nearDaysCount">Максимальное время путешествия в днях</param>
        /// <param name="maxSegmentCount">Максимальное количество пересадок</param>
        /// <param name="minHoursBetweenSegments">Минимальное время на пересадку в часах</param>
        /// <param name="maxHoursBetweenSegments">Максимальное время на пересадк в часах</param>
        /// <param name="advertDomain">Домен</param>
        /// <returns></returns>
        [OperationContract]
        public XElement TransferStations(string lang, string from, string to, string day, string month,
            string nearDaysCount,
            string maxSegmentCount, string minHoursBetweenSegments, string maxHoursBetweenSegments,
            string advertDomain = null)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "TransferStations?lang={lang}&from={from}&to={to}&month={month}&day={day}&nearDaysCount={nearDaysCount}&maxSegmentCount={maxSegmentCount}&minHoursBetweenSegments={minHoursBetweenSegments}&maxHoursBetweenSegments={maxHoursBetweenSegments}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"from", from},
                    {"to", to},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"nearDaysCount", nearDaysCount},
                    {"maxSegmentCount", maxSegmentCount},
                    {"minHoursBetweenSegments", minHoursBetweenSegments},
                    {"maxHoursBetweenSegments", maxHoursBetweenSegments}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Получение расписания движения поездов с пересадками с возможностью дальнейшей покупки билетов.
        /// </summary>
        /// <param name="lang">Язык</param>
        /// <param name="from">Откуда</param>
        /// <param name="to">Куда</param>
        /// <param name="day">День меняца</param>
        /// <param name="month">Месяц года</param>
        /// <param name="minHoursBetweenSegments">Минимальное время на пересадку в часах</param>
        /// <param name="maxHoursBetweenSegments">Максимальное время на пересадк в часах</param>
        /// <param name="advertDomain">Домен</param>
        /// <returns></returns>
        [OperationContract]
        public XElement TrainListTransfer(string lang, string from, string to, string transfer, string day, string month,
            string minHoursBetweenSegments, string maxHoursBetweenSegments)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate =
                    "TrainListTransfer?lang={lang}&from={from}&to={to}&transfer={transfer}&month={month}&day={day}&minHoursBetweenSegments={minHoursBetweenSegments}&maxHoursBetweenSegments={maxHoursBetweenSegments}";
                var dic = new Dictionary<string, string>
                {
                    {"lang", lang},
                    {"from", from},
                    {"to", to},
                    {"transfer",transfer},
                    {"month", (month ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"day", (day ?? "").ToString(CultureInfo.InvariantCulture)},
                    {"minHoursBetweenSegments", minHoursBetweenSegments},
                    {"maxHoursBetweenSegments", maxHoursBetweenSegments},
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        #region АСУМД (премиальные билеты)

        /// <summary>
        ///     Авторизовать пользователя в АСУМД
        /// </summary>
        /// <param name="account">
        ///     аккаунт пользователя, может содержать только 13-ти значный номер аккаунта, или строковую
        ///     переменную, напрмер логин администратора для сервиса реверса
        /// </param>
        /// <param name="password">пароль пользователя, может содержать любое строковое значение</param>
        /// <returns>Если удачно, возвращает токен, иначе - кидает исключение</returns>
        public XElement LoyaltyServiceAuthorize(string account, string password)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "LoyaltyServiceAuthorize?account={account}&password={password}";
                var dic = new Dictionary<string, string>()
                {
                    {"account", account},
                    {"password", password},
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        ///     Запрос на завершение сессии в АСУМД
        /// </summary>
        /// <param name="token">токен сессии пользователя</param>
        public XElement LoyaltyServiceLogout(string token)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "LoyaltyServiceLogout?token={token}";
                var dic = new Dictionary<string, string>()
                {
                    {"token", token},
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        ///     Запрос баланса пользователя в АСУМД
        /// </summary>
        /// <param name="token">токен сессии пользователя</param>
        /// <returns>Баланс пользователя, содержит любое число типа Long</returns>
        public XElement LoyaltyServiceGetBalance(string token)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "LoyaltyServiceGetBalance?token={token}";
                var dic = new Dictionary<string, string>()
                {
                    {"token", token},
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }


        /// <summary>
        ///     Откат покупки билетов в АСУМД
        /// </summary>
        /// <param name="token">токен сессии пользователя</param>
        /// <param name="transId">ID транзакции</param>
        public XElement LoyaltyServiceReverse(string token, int transId)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "LoyaltyServiceReverse?token={token}&transId={transId}";
                var dic = new Dictionary<string, string>()
                {
                    {"token", token},
                    {"transId", transId.ToString()},
                };
                var rm = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }

        #endregion


        /// <summary>
        /// добавление реферальных полей
        /// </summary>
        /// <param name="inputXml"></param>
        /// <returns></returns>
        public XElement SetAdditionalPassengersFieldPost(string inputXml)
        {
            return ExecutePost(inputXml, "SetAdditionalPassengersField?");
            ;
        }


        public XElement ReBooking(string idtrans)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "Rebooking?idtrans={idtrans}";
                var dic = new Dictionary<string, string>
                {
                    {"idtrans", idtrans}
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }


        public XElement GetBalance()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "GetBalance?";
                var dic = new Dictionary<string, string>{};
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }


        public XElement GetAvailableRouteDates(string from, string to, string train)
        {
            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 10, 0);
                var uriTemplate = "GetAvailableRouteDates?from={from}&to={to}&train={train}";
                var dic = new Dictionary<string, string>
                {
                    {"from", from},
                    {"to", to},
                    {"train", train},
                };
                var rm = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = UrlEncode(uriTemplate, dic)
                };
                SetCredential(rm);
                return XElement.Parse(client.SendAsync(rm).Result.Content.ReadAsStringAsync().Result);
            }
        }
    }
}