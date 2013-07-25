﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using AutoMapper;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Editors;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Models.Mapping;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using System.Linq;
using Umbraco.Web.WebApi.Binders;
using Umbraco.Web.WebApi.Filters;
using umbraco;

namespace Umbraco.Web.Editors
{

    //internal interface IUmbracoApiService<T>
    //{
    //    T Get(int id);
    //    IEnumerable<T> GetChildren(int id);
    //    HttpResponseMessage Delete(int id);
    //    //copy
    //    //move
    //    //update
    //    //create
    //}

    [PluginController("UmbracoApi")]
    public class MediaController : ContentControllerBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public MediaController()
            : this(UmbracoContext.Current)
        {            
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="umbracoContext"></param>
        internal MediaController(UmbracoContext umbracoContext)
            : base(umbracoContext)
        {
        }

        /// <summary>
        /// Gets an empty content item for the 
        /// </summary>
        /// <param name="contentTypeAlias"></param>
        /// <param name="parentId"></param>
        /// <returns></returns>
        public MediaItemDisplay GetEmpty(string contentTypeAlias, int parentId)
        {
            var contentType = Services.ContentTypeService.GetMediaType(contentTypeAlias);
            if (contentType == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            var emptyContent = new Core.Models.Media("Empty", parentId, contentType);
            return Mapper.Map<IMedia, MediaItemDisplay>(emptyContent);
        }

        /// <summary>
        /// Gets the content json for the content id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public MediaItemDisplay GetById(int id)
        {
            var foundContent = Services.MediaService.GetById(id);
            if (foundContent == null)
            {
                HandleContentNotFound(id);
            }
            return Mapper.Map<IMedia, MediaItemDisplay>(foundContent);
        }

        /// <summary>
        /// Returns the root media objects
        /// </summary>
        public IEnumerable<ContentItemBasic<ContentPropertyBasic, IMedia>> GetRootMedia()
        {
            return Services.MediaService.GetRootMedia()
                           .Select(Mapper.Map<IMedia, ContentItemBasic<ContentPropertyBasic, IMedia>>);
        }

        /// <summary>
        /// Returns the child media objects
        /// </summary>
        public IEnumerable<ContentItemBasic<ContentPropertyBasic, IMedia>> GetChildren(int parentId)
        {
            return Services.MediaService.GetChildren(parentId)
                           .Select(Mapper.Map<IMedia, ContentItemBasic<ContentPropertyBasic, IMedia>>);
        }

        /// <summary>
        /// Saves content
        /// </summary>
        /// <returns></returns>        
        [FileUploadCleanupFilter]
        public MediaItemDisplay PostSave(
            [ModelBinder(typeof(MediaItemBinder))]
                ContentItemSave<IMedia> contentItem)
        {
            //If we've reached here it means:
            // * Our model has been bound
            // * and validated
            // * any file attachments have been saved to their temporary location for us to use
            // * we have a reference to the DTO object and the persisted object

            UpdateName(contentItem);

            MapPropertyValues(contentItem);

            //We need to manually check the validation results here because:
            // * We still need to save the entity even if there are validation value errors
            // * Depending on if the entity is new, and if there are non property validation errors (i.e. the name is null)
            //      then we cannot continue saving, we can only display errors
            // * If there are validation errors and they were attempting to publish, we can only save, NOT publish and display 
            //      a message indicating this
            if (!ModelState.IsValid)
            {
                if (ValidationHelper.ModelHasRequiredForPersistenceErrors(contentItem)
                    && (contentItem.Action == ContentSaveAction.SaveNew))
                {
                    //ok, so the absolute mandatory data is invalid and it's new, we cannot actually continue!
                    // add the modelstate to the outgoing object and throw a 403
                    var forDisplay = Mapper.Map<IMedia, MediaItemDisplay>(contentItem.PersistedContent);
                    forDisplay.Errors = ModelState.ToErrorDictionary();
                    throw new HttpResponseException(Request.CreateResponse(HttpStatusCode.Forbidden, forDisplay));
                }
            }

            //save the item
            Services.MediaService.Save(contentItem.PersistedContent);

            //return the updated model
            var display = Mapper.Map<IMedia, MediaItemDisplay>(contentItem.PersistedContent);
            
            //lasty, if it is not valid, add the modelstate to the outgoing object and throw a 403
            HandleInvalidModelState(display);

            //put the correct msgs in 
            switch (contentItem.Action)
            {
                case ContentSaveAction.Save:
                case ContentSaveAction.SaveNew:
                    display.AddSuccessNotification(ui.Text("speechBubbles", "editMediaSaved"), ui.Text("speechBubbles", "editMediaSavedText"));
                    break;                
            }

            return display;
        }
    }
}
