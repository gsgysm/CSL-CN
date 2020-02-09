﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HanJie.CSLCN.Models.DataModels;
using HanJie.CSLCN.Models.Dtos;
using HanJie.CSLCN.Services;
using Microsoft.AspNetCore.Mvc;
using HanJie.CSLCN.WebApp.Attributes;
using HanJie.CSLCN.Common;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace HanJie.CSLCN.WebApp.Controllers
{
    [Route("api/[controller]")]
    public class WikiPassagesController : BaseController
    {
        private WikiPassageService _wikiPassageService { get; set; }

        public WikiPassagesController(WikiPassageService wikiPassageService,
            UserStatuService userStatuService) : base(userStatuService)
        {
            _wikiPassageService = wikiPassageService;
        }


        // GET api/<controller>/5
        [HttpGet("{id}")]
        public async Task<JsonResult> GetAsync(string id)
        {
            WikiPassage wikiPassage = await this._wikiPassageService.GetByRoutePathAsync(id);
            WikiPassageDto wikiPassageDto = new WikiPassageDto().ConvertFromDataModel(wikiPassage);
            wikiPassageDto.AnchorTitles = await this._wikiPassageService.CollectAnchorTitlesAsync(wikiPassageDto.Content);
            wikiPassageDto.MainAuthors = this._wikiPassageService.CollectAuthorInfoes(wikiPassage.MainAuthors.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            wikiPassageDto.CoAuthors = wikiPassage.CoAuthors != null ? this._wikiPassageService.CollectAuthorInfoes(wikiPassage.CoAuthors?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) : null;
            wikiPassageDto.BreadCrumbs = wikiPassage.ParentPassageId != 0 ? this._wikiPassageService.CollectBreadCrumbs(wikiPassageDto) : null;
            wikiPassageDto.ChildPageBreadCrumbs = await this._wikiPassageService.CollectChildPageBreadCrumbs(wikiPassageDto);

            return new JsonResult(wikiPassageDto);
        }

        [HttpGet]
        [Route("/api/wikipassages/isduplicated")]
        public async Task<IActionResult> IsDuplicated(string routePath)
        {
            Ensure.NotNull(routePath, nameof(routePath));

            bool result = await this._wikiPassageService.IsRoutePathExist(routePath);
            return Json(result);
        }

        // POST api/<controller>
        [HttpPost]
        [MyAuthorize]
        public async Task<IActionResult> PostAsync([FromBody]WikiPassageDto dto)
        {
            dto.MainAuthors = new List<UserInfoDto> { base.CurrentUser };
            WikiPassage wikiPassage = await this._wikiPassageService.Create(dto);
            WikiPassageDto wikiPassageDto = new WikiPassageDto().ConvertFromDataModel(wikiPassage);

            return Json(wikiPassageDto);
        }

        // PUT api/<controller>/5
        [HttpPut]
        [MyAuthorize]
        public async Task Put([FromBody]WikiPassageDto dto)
        {
            await this._wikiPassageService.UpdateAsync(dto);
        }

        // DELETE api/<controller>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }


    }
}
