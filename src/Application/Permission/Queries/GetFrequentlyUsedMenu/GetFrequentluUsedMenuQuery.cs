﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation.Results;
using MediatR;
using Wisework.ConsentManagementSystem.Api;
using WW.Application.Common.Exceptions;
using WW.Application.Common.Interfaces;

namespace WW.Application.Permission.Queries.GetFrequentlyUsedMenu;


public record GetFrequentluUsedMenuQuery(int count) : IRequest<List<FrequentlyUsedMenu>>;

public class GetFrequentluUsedMenuQueryHandler : IRequestHandler<GetFrequentluUsedMenuQuery, List<FrequentlyUsedMenu>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetFrequentluUsedMenuQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<FrequentlyUsedMenu>> Handle(GetFrequentluUsedMenuQuery request, CancellationToken cancellationToken)
    {
        var actions = _context.DbSetAction;
        var programs = _context.DbSetProgram;
        var programActions = _context.DbSetProgramAction;
        var programDescriptions = _context.DbSetProgramDescription;
        var roles = _context.DbSetRole;
        var roleProgramActions = _context.DbSetRoleProgramAction;
        var userRoles = _context.DbSetUserRole;

        string baseUrl = "https://test-pdpa.thewiseworks.com/";
        string language = "en-US";
        List<int> roleIDs = new List<int> { 4, 50168, 50169 };
        int companyId = 1;

        try
        {
            if (request.count < 0)
            {
                List<ValidationFailure> failures = new List<ValidationFailure> { };

                failures.Add(new ValidationFailure("count", "count must be greater than 0"));

                throw new ValidationException(failures);
            }

            var menus = (from p1 in programs
                         join pd in programDescriptions.Where(d => d.LanguageCulture == language)
                         on p1.ProgramID equals pd.ProgramID into pdGroup
                         from pd in pdGroup.DefaultIfEmpty()
                         where p1.Status == "A" && (
                            (from rpa in roleProgramActions
                             join r in roles on rpa.RoleID equals r.RoleID
                             join pa in programActions on rpa.ProgramActionID equals pa.ProgramActionID
                             join p2 in programs on pa.ProgramID equals p2.ProgramID
                             join a in actions on pa.ActionID equals a.ActionID
                             where roleIDs.Contains(Convert.ToInt32(rpa.RoleID)) && r.CompanyID == companyId && p2.Code == p1.Code && a.Code == "VIEW" && r.Status != "X"
                             select 1
                             ).Count() > 0
                         ) || p1.Code == "HOME_MAIN"
                         orderby p1.Priority
                         select new FrequentlyUsedMenu
                         {
                             Action = baseUrl + p1.Action,
                             Code = p1.Code,
                             Description = p1.Description,
                             ParentID = p1.ParentID,
                             Priority = p1.Priority,
                             ProgramGroupID = 20, // what?
                             ProgramID = p1.ProgramID,
                             IsExpanded = p1.expanded == null || p1.expanded == 0 ? false : true,
                             Icon = p1.Icon,
                         }
                    ).Take(request.count).ToList();

            //if (menus == null) return new List<FrequentlyUsedMenu>();

            
            if (menus == null)
            {
                throw new NotFoundException("User not found"); // 404 Not Found
            }

            /*

            SELECT * FROM [Action];
            SELECT * FROM [Program];
            SELECT * FROM [ProgramAction];
            SELECT * FROM [ProgramDescription];
            SELECT * FROM [Role];
            SELECT * FROM [RoleProgramAction];

            SELECT 
                P1.ProgramId, P1.Code, P1.Description, P1.ParentID, 
                P1.Action, P1.Icon, P1.Badge, P1.expanded, P1.Priority
            FROM [Program] P1
            LEFT JOIN (
                SELECT * FROM [ProgramDescription] WHERE LanguageCulture = 'en-US'
            ) PD ON PD.ProgramID = P1.ProgramID
            WHERE P1.Status = 'A'
            AND (
                (
                    SELECT (CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END)
                    FROM [RoleProgramAction] RPA
                    LEFT JOIN [Role] R ON R.RoleID = RPA.RoleID
                    LEFT JOIN [ProgramAction] PA ON PA.ProgramActionID = RPA.ProgramActionID
                    LEFT JOIN [Program] P2 ON P2.ProgramID = PA.ProgramID
                    LEFT JOIN [Action] A ON A.ActionID = PA.ActionID
                    WHERE RPA.RoleID IN (4, 50168, 50169)
                    AND R.CompanyID = 1
                    AND P2.Code = P1.Code
                    AND A.Code = 'VIEW'
                    AND R.Status != 'X'
                ) = 1
                OR (P1.Code = 'HOME_MAIN')
            )
            ORDER BY P1.Priority;

            */

            return menus;
        }
        catch (NotFoundException)
        {
            throw new NotFoundException("User not found"); // 404 Not Found
        }
        catch (ValidationException)
        {
            List<ValidationFailure> failures = new List<ValidationFailure> { };

            failures.Add(new ValidationFailure("count", "count must be greater than 0"));

            throw new ValidationException(failures);
        }

        catch (Exception ex)
        {
            throw new InternalException("An internal server error occurred."); // 500 error
        }



    }
}
