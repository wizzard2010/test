using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Http;
using System.Xml.Linq;
using Lan.HttpApi.Core;
using Lan.HttpClient.Core;
using Lan.Logging;
using Lan.ThesaurusApiService.Data;
using Lan.ThesaurusApiService.Dto;
using Lan.ThesaurusApiService.Dto.Entities;
using Lan.ThesaurusApiService.Dto.Methods;
using Lan.ThesaurusApiService.Dto.Methods.AddDescriptor;
using Lan.ThesaurusApiService.Dto.Methods.AddDescriptor2;
using Lan.ThesaurusApiService.Dto.Methods.AddHighLevelThesaurusDescriptor;
using Lan.ThesaurusApiService.Dto.Methods.ExportTagsHierarchy;
using Lan.ThesaurusApiService.Dto.Methods.GetDescription;
using Lan.ThesaurusApiService.Dto.Methods.GetDescriptorRelations;
using Lan.ThesaurusApiService.Dto.Methods.GetHierachy;
using Lan.ThesaurusApiService.Dto.Methods.GetOneLevelDescriptors;
using Lan.ThesaurusApiService.Dto.Methods.GetTagServices;
using Lan.ThesaurusApiService.Dto.Methods.GetThesauri;
using Lan.ThesaurusApiService.Dto.Methods.GetWordsList;
using Lan.ThesaurusApiService.Dto.Methods.ImportTagsHierarchy;
using Lan.ThesaurusApiService.Dto.Methods.SearchWord;
using Lan.ThesaurusApiService.Dto.Methods.SearchWords;
using Lan.ThesaurusApiService.ExportImport;
using Lan.ThesaurusApiService.ExportImport.Accessors;

namespace ThesaurusApiService.Controllers
{
    using Lan.ThesaurusApiService.Dto.Methods.CreateDescriptorWithOldNameAsAlternativeWord;
    using Lan.ThesaurusApiService.Dto.Methods.GetDescriptorsWithAlternativesAsScatQLQuery;
    using Lan.ThesaurusApiService.Dto.Methods.GetRelationTypes;
    using Lan.ThesaurusApiService.Dto.Methods.GetSearchedDescriptorsForAutocompleteFromThesaurusesForSearch;

    [LoggerName("ThesaurusApiService")]
    public class ThesaurusController : BaseApiController
    {
        protected readonly IThesaurusRepository Repository;
        protected readonly ServiceLayer Service;
        protected readonly ThesaurusImportExportService ImportExportService;

        public ThesaurusController()
        {
            // TODO: Использовать Ninject.
            var thesauriConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Thesauri"].ConnectionString;

	        Repository = new ThesaurusRepository(thesauriConnectionString, Log);
	        ThesaurusAccessor thesaurusAccessor = new ThesaurusAccessor(thesauriConnectionString);


			Service = new ServiceLayer(Repository, Log, thesaurusAccessor);
            Version = new ThesaurusServiceVersion();

            var configValue = WebConfigurationManager.AppSettings["SharedFolderForStoringLargeExportFiles"];

            var folderNameForStoringLargeFile = string.IsNullOrEmpty(configValue) 
                ? (Path.Combine(HostingEnvironment.ApplicationPhysicalPath, "App_DATA", "temp")) 
                : configValue;

            ImportExportService = new ThesaurusImportExportService(thesaurusAccessor, folderNameForStoringLargeFile);
        }

        [HttpGet]
        public async Task<GetDescriptionResponse> GetDescription([FromUri]GetDescriptionRequest request)
        {
            return await Invoke(request, Repository.GetDescription);
        }

        /// <summary>
        /// Экспорт тегов со всеми их связями в xml-формате.
        /// </summary>
        /// <param name="request">Запрос со списком тегов.</param>
        /// <returns> Ответ, содержащий xml-документ</returns>
        [HttpPost]
        public ExportHierarchyResponse ExportTagsHierarchy(ExportTagsHierarchyRequest request)
        {
            XElement xml;
            try
            {
                xml = ImportExportService.ExportTagsToXml(request.ThesaurusId, request.TagIds);
            }
            catch (Exception e)
            {
                return new ExportHierarchyResponse
                {
                    Error = "Ошибка экспорта иерархии тегов.",
                    ExceptionType = e.GetType().FullName,
                    Message = e.Message
                };
            }
            
            return new ExportHierarchyResponse { Xml = xml };
        }

        /// <summary>
        /// Импорт тегов, полученных в xml-формате вызовом ExportTagsHierarchy.
        /// </summary>
        /// <param name="request">Запрос, содержащий xml-документ</param>
        /// <returns></returns>
        [HttpPost]
        public ImportTagsHierarchyResponse ImportTagsHierarchy(ImportTagsHierarchyRequest request)
        {
            List<PageTag> importedTags;
            try
            {
                importedTags = ImportExportService.ImportTagsToThesaurus(request.ThresaurusId, request.Xml);
            }
            catch (Exception e)
            {
                return new ImportTagsHierarchyResponse
                {
                    Error = "Ошибка импорта иерархии тегов.",
                    ExceptionType = e.GetType().FullName,
                    Message = e.Message
                };
            }
            
            return new ImportTagsHierarchyResponse { Tags = importedTags };
        }

        [HttpPost]
        public async Task<GetDescriptorsAsyncResponse> GetDescriptorsAsync(GetDescriptorsAsyncRequest request)
        {
            return await Invoke(request, Repository.GetDescriptorsAsync);
        }

        [HttpGet]
        public async Task<GetThesauriResponse> GetThesauri([FromUri]GetThesauriRequest request)
        {
            return await Invoke(request, Repository.GetThesauri);
        }

        [HttpGet]
        public async Task<SearchWordResponse> SearchWord([FromUri] SearchWordRequest request)
        {
            return await Invoke(request, Repository.SearchWord);
        }

        [HttpGet]
        public async Task<GetHierachyResponse> GetHierachy([FromUri] GetHierachyRequest request)
        {
            return await Invoke(request, Repository.GetHierachy);
        }

        [HttpGet]
        public async Task<AddDescriptorResponse> AddDescriptor([FromUri] AddDescriptorRequest request)
        {
            return await Invoke(request, Service.AddDescriptor);
        }

	    [HttpGet]
	    public async Task<AddDescriptor2Response> AddDescriptor2([FromUri] AddDescriptorRequest request)
	    {
		    return await Invoke(request, Service.AddDescriptor2);
	    }

	    [HttpGet]
        public async Task<AddHighLevelThesaurusDescriptorResponse> AddHighLevelThesaurusDescriptor([FromUri] AddHighLevelThesaurusDescriptorRequest request)
        {
            return await Invoke(request, Service.AddHighLevelThesaurusDescriptor);
        }

        [HttpGet]
        public async Task<GetTagServicesResponse> GetTagServices([FromUri] BaseRequest request)
        {
            return await Invoke(request, Repository.GetTagServices);
        }

        [HttpGet]
        public async Task<GetOneLevelDescriptorsResoponse> GetOneLevelDescriptors([FromUri] GetOneLevelDescriptorsRequest request)
        {
            return await Invoke(request, Service.GetOneLevelDescriptors);
        }

        [HttpGet]
        public async Task<GetDescriptorRelationsResponse> GetDescriptorRelations([FromUri] GetDescriptorRelationsRequest request)
        {
            return await Invoke(request, Service.GetDescriptorRelations);
        }

		[HttpGet]
	    public async Task<GetWordsListResponse> GetWordsList([FromUri] GetWordsListRequest request)
	    {
		    return await Invoke(request, Repository.GetWordsList);
	    }

	    [HttpGet]
		public async Task<SearchWordsResponse> SearchWords([FromUri] SearchWordsRequest request)
	    {
		    return await Invoke(request, Repository.SearchWords);
	    }

	    public async Task<GetRelationTypesResponse> GetRelationTypes([FromUri] GetRelationTypesRequest request)
	    {
		    return await Invoke(request, Service.GetRelationTypes);
	    }

        [HttpGet]
        public async Task<CreateDescriptorWithOldNameAsAlternativeWordResponse> CreateDescriptorWithOldNameAsAlternativeWord(
            [FromUri] CreateDescriptorWithOldNameAsAlternativeWordRequest request)
        {
            return await Invoke(request, Service.CreateDescriptorWithOldNameAsAlternativeWord);
        }

        [HttpGet]
        public async Task<GetDescriptorsWithAlternativesAsScatQLQueryResponse> GetDescriptorsWithAlternativesAsScatQLQuery(
            [FromUri] GetDescriptorsWithAlternativesAsScatQLQueryRequest request)
        {
            return await Invoke(request, Service.GetDescriptorsWithAlternativesAsScatQLQuery);
        }

        [HttpGet]
        public async Task<GetSearchedDescriptorsForAutocompleteFromThesaurusesForSearchResponse>
            GetSearchedDescriptorsForAutocompleteFromThesaurusesForSearch(
                [FromUri] GetSearchedDescriptorsForAutocompleteFromThesaurusesForSearchRequest request)
        {
            return await Invoke(request, Service.GetSearchedDescriptorsForAutocompleteFromThesaurusesForSearch);
        }
    }
}
