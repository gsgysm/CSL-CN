﻿using HanJie.CSLCN.Common;
using HanJie.CSLCN.Models.Consts;
using HanJie.CSLCN.Models.DataModels;
using HanJie.CSLCN.Models.Dtos;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;
using Qiniu.Storage;
using Qiniu.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HanJie.CSLCN.Services
{
    public class QiniuService : BaseService<QiniuStorageInfoDto, QiniuStorageInfo>
    {
        private readonly Mac _mac;
        private readonly List<string> _validQiniuCallBackUrls;
        private readonly string _qiniuCallBackUrl;

        public QiniuService()
        {
            this._mac = new Mac(GlobalConfigs.AppSettings.QiniuConfig.AccessKey, GlobalConfigs.AppSettings.QiniuConfig.SecretKey);
            this._validQiniuCallBackUrls = new List<string>() { GlobalConfigs.AppSettings.QiniuConfig.CallBackUrl, "http://www.cities-skylines.cn/api/qiniucallbacktest" };
            this._qiniuCallBackUrl = RunAs.Release ? GlobalConfigs.AppSettings.QiniuConfig.CallBackUrl : "http://www.cities-skylines.cn/api/qiniucallbacktest";
        }


        public string GetUploadToken(string storageFullName)
        {
            Ensure.NotNull(storageFullName, nameof(storageFullName));

            PutPolicy putPolicy = new PutPolicy();
            putPolicy.Scope = $"{GlobalConfigs.AppSettings.QiniuConfig.BucketName}:{storageFullName}";
            putPolicy.CallbackUrl = this._qiniuCallBackUrl;
            putPolicy.CallbackBody = JsonConvert.SerializeObject(new
            {
                FullName = "$(key)",                     //文件保存在空间中的资源名
                Hash = "$(etag)",                        //hash
                FileSize = "$(fsize)",                   //文件大小，单位为字节
                MimeType = "$(mimeType)",                //媒体类型 img/jpeg,
                ImageWidth = "$(imageInfo.width)",      //图片宽度
                ImageHeight = "$(imageInfo.height)"      //图片高度
            });
            putPolicy.CallbackBodyType = HttpConsts.ApplicationJson;
            string token = Auth.CreateUploadToken(this._mac, putPolicy.ToJsonString());

            return token;
        }

        public bool AuthCallBackHeader(string contentType, string authorization, string callBackUrl, string callBackBody)
        {
            //傻屌七牛 8.0.0 未支持 .net core 版本的 SDK 回调鉴权，又不提供手动鉴权的说明文档，服。

            //如果你能看到这段鉴权源码，请不要瞎搞这个回调口子，谢谢。

            bool isAccess = false;

            Ensure.NotNull(contentType, nameof(contentType));
            Ensure.NotNull(authorization, nameof(authorization));
            Ensure.NotNull(callBackUrl, nameof(callBackUrl));
            Ensure.NotNull(callBackBody, nameof(callBackBody));

            bool isContentTypeOK = false;
            if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
                isContentTypeOK = true;

            bool isAuthorizationOK = false;
            if (authorization.StartsWith("QBox", StringComparison.OrdinalIgnoreCase)
                && authorization.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() == GlobalConfigs.AppSettings.QiniuConfig.AccessKey)
                isAuthorizationOK = true;

            bool isCallBackUrlOK = false;
            if (this._validQiniuCallBackUrls.Contains(callBackUrl))
                isCallBackUrlOK = true;

            bool isCallBackBodyOK = false;
            if (callBackBody.ToLower().Contains("fullname")
                && callBackBody.ToLower().Contains("hash")
                && callBackBody.ToLower().Contains("filesize")
                && callBackBody.ToLower().Contains("mimetype")
                && callBackBody.ToLower().Contains("imagewidth")
                && callBackBody.ToLower().Contains("imageheight"))
                isCallBackBodyOK = true;

            if (isContentTypeOK && isAuthorizationOK && isCallBackUrlOK && isCallBackBodyOK)
                isAccess = true;

            return isAccess;
        }

        public async virtual Task<string> CallBackHandler(string contentType, string authorization, string callBackUrl, string callBackBody)
        {
            bool isAccess = AuthCallBackHeader(contentType, authorization, callBackUrl, callBackBody);

            if (!isAccess)
                return JsonConvert.SerializeObject(new { ret = "failure" });

            QiniuStorageInfo qiniuStorageInfo = new QiniuStorageInfo().ConvertFromDtoModel(JsonConvert.DeserializeObject<QiniuStorageInfoDto>(callBackBody));
            QiniuStorageInfoDto dto = new QiniuStorageInfoDto().ConvertFromDataModel(await UpdateFileStorageInfo(qiniuStorageInfo));

            return JsonConvert.SerializeObject(new { ret = "success", info = dto });

        }

        public virtual async Task<QiniuStorageInfo> UpdateFileStorageInfo(QiniuStorageInfo qiniuStorageInfo)
        {
            Ensure.NotNull(qiniuStorageInfo, nameof(qiniuStorageInfo));

            QiniuStorageInfo entityToUpdate = this.CSLDbContext.QiniuStorageInfoes.Where(item => item.FullName == qiniuStorageInfo.FullName).FirstOrDefault();

            if (entityToUpdate != null)
                this.CSLDbContext.QiniuStorageInfoes.Remove(entityToUpdate);

            qiniuStorageInfo.CreateDate = DateTime.Now;
            qiniuStorageInfo.LastModifyDate = DateTime.Now;
            EntityEntry<QiniuStorageInfo> entry = await this.CSLDbContext.QiniuStorageInfoes.AddAsync(qiniuStorageInfo);
            await this.CSLDbContext.SaveChangesAsync();

            return entry.Entity;
        }

    }
}