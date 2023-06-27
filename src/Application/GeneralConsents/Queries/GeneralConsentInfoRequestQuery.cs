﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Wisework.ConsentManagementSystem.Api;
using WW.Application.Common.Exceptions;
using WW.Application.Common.Interfaces;
using WW.Application.Common.Models;
using WW.Domain.Common;
using WW.Domain.Entities;
using WW.Domain.Enums;

namespace WW.Application.GeneralConsents.Queries;

public record GeneralConsentInfoRequestQuery : IRequest<GeneralConsent>
{
    public string IdCardNumber { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string CollectionPointGuid { get; set; }
    [JsonIgnore]
    public AuthenticationModel? authentication { get; set; }
}

public class GeneralConsentInfoRequestQueryHandler : IRequestHandler<GeneralConsentInfoRequestQuery, GeneralConsent>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GeneralConsentInfoRequestQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<GeneralConsent> Handle(GeneralConsentInfoRequestQuery request, CancellationToken cancellationToken)
    {
        if (request.authentication == null)
        {
            throw new UnauthorizedAccessException();
        }
        try
        {
            if (request.CollectionPointGuid == null || request.CollectionPointGuid == "")
            {
                throw new Exception("CollectionPointGUID is required");
            }

            if (request.FullName == null
                //&& request.FullName == "" 
                && request.IdCardNumber == null
                //&& request.IdCardNumber == "" 
                && request.PhoneNumber == null
                //&& request.PhoneNumber == ""
                && request.Email == null
                //&& request.Email == ""
                && request.CollectionPointGuid == null
                //&& request.CollectionPointGuid == ""
                )
            {
                return null;
            }
            var query = (from c in _context.DbSetConsent
                         join cp in _context.DbSetConsentCollectionPoints on c.CollectionPointId equals cp.CollectionPointId
                         join w in _context.DbSetConsentWebsite on c.WebsiteId equals w.WebsiteId
                         join company in _context.DbSetCompanies on c.CompanyId.Value equals company.CompanyId
                         join uCreate in _context.DbSetUser on cp.CreateBy equals uCreate.CreateBy
                         join uUpdate in _context.DbSetUser on cp.CreateBy equals uUpdate.UpdateBy
                         where c.CompanyId == 1 && c.New == 1
                         && cp.Guid == request.CollectionPointGuid
                         select new GeneralConsent
                         {
                             ConsentId = c.ConsentId,
                             CollectionPointId = c.CollectionPointId,
                             Uid = c.Uid,
                             TotalTransactions = c.TotalTransactions,
                             FullName = c.FullName,
                             CollectionPointGuid = cp.Guid,
                             ConsentDateTime = c.ConsentDatetime.ToString(),
                             //Website = "",
                             CollectionPointVersion = cp.Version,
                             //PurposeList = "",
                             FromBrowser = c.FromBrowser,
                             PhoneNumber = c.PhoneNumber,
                             IdCardNumber = c.IdCardNumber,
                             Email = c.Email,
                             Remark = c.Remark,
                             CompanyId = c.CompanyId,
                             CompanyName = company.Name,
                             Status = c.Status,
                             //EventCode = c.EventCode, <- dll.TotalCount 
                             VerifyType = c.VerifyType,
                         });

            #region Filter consent primary key

            if (request.FullName != null && request.FullName != "")
            {
                query = query.Where(p => p.FullName == request.FullName);
            }
            if (request.IdCardNumber != null && request.IdCardNumber != "")
            {
                query = query.Where(p => p.IdCardNumber == request.IdCardNumber);
            }
            if (request.PhoneNumber != null && request.PhoneNumber != "")
            {
                query = query.Where(p => p.PhoneNumber == request.PhoneNumber);
            }
            if (request.Email != null && request.Email != "")
            {
                query = query.Where(p => p.Email == request.Email);
            }

            #endregion
            var model = query.FirstOrDefault();

            var consentIdIds = query.Select(c => c.ConsentId);

            var collectionPointIds = query.Select(c => c.CollectionPointId);


            if (consentIdIds.ToList().Count > 0)
            {
                #region fetch website
                var websiteList = (from cp in _context.DbSetConsentCollectionPoints
                                   join w in _context.DbSetConsentWebsite on cp.WebsiteId equals w.WebsiteId
                                   where collectionPointIds.Contains(cp.CollectionPointId)
                                   select
                                       new Website4
                                       {
                                           Id = w.WebsiteId,
                                           Description = w.Description,
                                           UrlHomePage = w.Url,
                                           UrlPolicyPage = w.Urlpolicy,
                                       }).ToList();

                model.Website = websiteList.FirstOrDefault();
                //var websiteLookup = websiteList.ToLookup(item => item.Key, item => item.Value);
                #endregion
                #region fetch purpose
                var purposeLists = (from cp in _context.DbSetConsentCollectionPointItem
                                    join p in _context.DbSetConsentPurpose on cp.PurposeId equals p.PurposeId
                                    where cp.Status == Status.Active.ToString()
                                    && p.Status == Status.Active.ToString()
                                    && collectionPointIds.Contains(cp.CollectionPointId)
                                    select
                                        new GeneralConsentPurpose
                                        {
                                            PurposeId = p.PurposeId,
                                            CompanyId = p.CompanyId,
                                            Code = p.Code,
                                            Description = p.Description,
                                            WarningDescription = p.WarningDescription,
                                            PurposeCategoryId = p.PurposeCategoryId,
                                            ExpiredDateTime = Calulate.ExpiredDateTime(p.KeepAliveData, p.CreateDate),
                                            Guid = new Guid(p.Guid),
                                            Version = p.Version,
                                            Priority = cp.Priority,
                                            Status = p.Status
                                        }).ToList();

                //var purposeLookup = purposeLists.ToLookup(item => item.Key, item => item.Value);
                #endregion

                //var purposeList = (from c in _context.DbSetConsent
                //join ci in _context.DbSetConsentItem on c.ConsentId equals ci.ConsentId
                //join cp in _context.DbSetConsentCollectionPointItem on ci.CollectionPointItemId equals cp.CollectionPointItemId
                //join p in _context.DbSetConsentPurpose on cp.PurposeId equals p.PurposeId
                //where c.CompanyId == 1
                //&& c.New == 1
                //&& consentIdIds.Contains(c.ConsentId)
                //select
                //    new GeneralConsentPurpose
                //    {
                //        PurposeId = p.PurposeId,
                //        CompanyId = p.CompanyId,
                //        Code = p.Code,
                //        Description = p.Description,
                //        WarningDescription = p.WarningDescription,
                //        PurposeCategoryId = p.PurposeCategoryId,
                //        ExpiredDateTime = Calulate.ExpiredDateTime(p.KeepAliveData, p.CreateDate),
                //        Guid = new Guid(p.Guid),
                //        Version = p.Version,
                //        Priority = cp.Priority,
                //        Status = p.Status
                //    }).ToList();

                model.PurposeList = purposeLists;
            }


            return model;
        }
        catch (Exception ex)
        {
            throw new InternalServerException(ex.Message);

        }
    }
}
