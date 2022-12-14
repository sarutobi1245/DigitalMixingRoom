using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DMR_API.Helpers;
using AutoMapper;
using DMR_API._Repositories.Interface;
using DMR_API._Services.Interface;
using DMR_API.DTO;
using DMR_API.Models;
using Microsoft.EntityFrameworkCore;

using DMR_API._Repositories.Repositories;
using DMR_API.Data;

namespace DMR_API._Services.Services
{
    public class PermissionService : IPermissionService
    {
        #region Ctor

        private readonly IPermissionRepository _repoPermission;

        private readonly IMapper _mapper;
        private readonly IActionRepository _repoAction;
        private readonly IModuleRepository _repoModule;
        private readonly IRoleRepository _repoRole;
        private readonly ILanguageRepository _repoLanguage;
        private readonly IModuleTranslationRepository _repoModuleTranslation;
        private readonly IFunctionTranslationRepository _repoFunctionTranslation;
        private readonly IFunctionSystemRepository _repoFunctionSystem;
        private readonly IActionInFunctionSystemRepository _repoActionInFunctionSystem;
        private readonly IUserRoleRepository _repoUserRole;
        private readonly DataContext _context;
        private readonly MapperConfiguration _configMapper;
        private readonly string[] Permissions = new string[] { "Action", "Action In Function", "Module", "Function" };
        public PermissionService(
            IPermissionRepository repoPermission,
            IMapper mapper,
            IActionRepository repoAction,
            DataContext context,
            IModuleRepository repoModule,
            IRoleRepository repoRole,
            ILanguageRepository repoLanguage,
            IModuleTranslationRepository repoModuleTranslation,
            IFunctionTranslationRepository repoFunctionTranslation,
            IFunctionSystemRepository repoFunctionSystem,
            IActionInFunctionSystemRepository repoActionInFunctionSystem,
            IUserRoleRepository repoUserRole,
            MapperConfiguration configMapper)
        {
            _configMapper = configMapper;
            _mapper = mapper;
            _repoAction = repoAction;
            _repoModule = repoModule;
            _repoRole = repoRole;
            _context = context;
            _repoLanguage = repoLanguage;
            _repoModuleTranslation = repoModuleTranslation;
            _repoFunctionTranslation = repoFunctionTranslation;
            _repoFunctionSystem = repoFunctionSystem;
            _repoActionInFunctionSystem = repoActionInFunctionSystem;
            _repoUserRole = repoUserRole;
            _repoPermission = repoPermission;
        }

        #endregion

        #region Module
        public async Task<object> GetModulesAsTreeView()
        {
            var model = (await _repoModule.FindAll()
                .Include(x => x.ModuleTranslations)
                .OrderBy(x => x.Sequence).ToListAsync())
                .Select((x, i) => new HierarchyNode<ModuleTreeDto>
                {
                    Entity = new ModuleTreeDto
                    {
                        Index = ++i,
                        ID = x.ID,
                        Code = x.Code,
                        Name = x.Name == "" || x.Name == null ? x.ModuleTranslations.First(x => x.LanguageID == "en").Name : x.Name,
                        Icon = x.Icon,
                        Url = x.Url,
                        Sequence = x.Sequence,
                        LanguageID = "",
                        Level = 1,
                    },
                    ChildNodes = x.ModuleTranslations != null ? x.ModuleTranslations.Select(a => new HierarchyNode<ModuleTreeDto>
                    {
                        Entity = new ModuleTreeDto
                        {
                            ID = a.ID,
                            Name = a.Name,
                            Icon = x.Icon,
                            Url = x.Url,
                            Sequence = x.Sequence,
                            ParentID = a.ModuleID,
                            LanguageID = a.LanguageID,
                            Level = 2
                        },
                    }).ToList() : new List<HierarchyNode<ModuleTreeDto>>()
                }).ToList();

            return model;
        }
        public async Task<ResponseDetail<object>> UpdateModule(ModuleDto moduleDto)
        {

            try
            {
                var module = _mapper.Map<Module>(moduleDto);
                _repoModule.Update(module);
                var result = await _repoModule.SaveAll();
                foreach (var translation in moduleDto.Translations)
                {
                    var item = await _repoModuleTranslation.FindAll(x => x.ModuleID == module.ID && x.LanguageID == translation.LanguageID).FirstOrDefaultAsync();
                    item.Name = translation.Name;
                    item.LanguageID = translation.LanguageID;
                    _repoModuleTranslation.Update(item);
                    await _repoModuleTranslation.SaveAll();
                }

                return new ResponseDetail<object> { Status = true };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }

        }

        public async Task<ResponseDetail<object>> DeleteModule(int moduleID)
        {
            var module = await _repoModule.FindAll(x => x.ID == moduleID)
                .Include(x => x.ModuleTranslations).FirstOrDefaultAsync();
            try
            {
                if (module.ModuleTranslations.Count > 0)
                {
                    _repoModuleTranslation.RemoveMultiple(module.ModuleTranslations.ToList());
                }
                _repoModule.Remove(module);
                var result = await _repoModule.SaveAll();
                return new ResponseDetail<object> { Status = result };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }
        public async Task<ResponseDetail<object>> DeleteModuleTranslation(int moduleTranslationID)
        {
            try
            {
                var moduleTranslation = await _repoModuleTranslation.FindAll(x => x.ID == moduleTranslationID)
                    .FirstOrDefaultAsync();
                _repoModuleTranslation.Remove(moduleTranslation);
                var result = await _repoModuleTranslation.SaveAll();
                return new ResponseDetail<object> { Status = result };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }

        public async Task<ResponseDetail<object>> AddModule(ModuleDto moduleDto)
        {

            try
            {
                var module = _mapper.Map<Module>(moduleDto);
                module.CreatedTime = DateTime.Now;
                _repoModule.Add(module);
                var result = await _repoModule.SaveAll();

                var list = new List<ModuleTranslation>();
                foreach (var translation in moduleDto.Translations)
                {
                    var item = translation;
                    list.Add(new ModuleTranslation(module.ID, item.Name, item.LanguageID));
                }
                _repoModuleTranslation.AddRange(list);
                await _repoModuleTranslation.SaveAll();

                return new ResponseDetail<object> { Status = true };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }


        #endregion

        #region Function
        public async Task<IEnumerable<HierarchyNode<FunctionSystem>>> GetFunctionsAsTreeViewV1()
        {
            var model = (await _repoFunctionSystem.FindAll().Include(x => x.Module).Include(x => x.Function).OrderBy(x => x.ID).OrderBy(x => x.ModuleID).ThenBy(x => x.Sequence).ToListAsync()).AsHierarchy(x => x.ID, y => y.ParentID);
            return model;
        }

        public async Task<IEnumerable<HierarchyNode<FunctionTreeDto>>> GetFunctionsAsTreeView()
        {
            var parents = (await _repoFunctionSystem.FindAll()
                .Include(x => x.Module)
                .Include(x => x.FunctionTranslations)
                .OrderBy(x => x.ID)
                .OrderBy(x => x.ModuleID)
                .ThenBy(x => x.Sequence)
                .ToListAsync())
                .Select((x, i) => new HierarchyNode<FunctionTreeDto>
                {
                    Entity = new FunctionTreeDto
                    {
                        ID = x.ID,
                        Index = ++i,
                        Name = x.Name == "" || x.Name == null ? x.FunctionTranslations.First(x => x.LanguageID == "en").Name : x.Name,
                        Code = x.Code,
                        Icon = x.Icon,
                        Url = x.Url,
                        Sequence = x.Sequence,
                        ModuleID = x.ModuleID,
                        ModuleName = x.Module.Name,
                        LanguageID = "",
                        Level = 1,
                        ParentID = null,
                    },
                    ChildNodes = x.FunctionTranslations != null ? x.FunctionTranslations.Select(x => new HierarchyNode<FunctionTreeDto>
                    {
                        Entity = new FunctionTreeDto
                        {
                            ID = x.ID,
                            Name = x.Name,
                            Level = 2,
                            LanguageID = x.LanguageID,
                            ParentID = x.FunctionSystemID
                        },
                    }).ToList() : new List<HierarchyNode<FunctionTreeDto>>()
                })
                .ToList();

            return parents;
        }
        public async Task<ResponseDetail<object>> UpdateFunction(FunctionDto functionDto)
        {

            try
            {
                var function = _mapper.Map<FunctionSystem>(functionDto);
                _repoFunctionSystem.Update(function);
                await _repoFunctionSystem.SaveAll();

                foreach (var translation in functionDto.Translations)
                {
                    var item = await _repoFunctionTranslation.FindAll(x => x.FunctionSystemID == function.ID && x.LanguageID == translation.LanguageID).FirstOrDefaultAsync();
                    item.Name = translation.Name;
                    item.LanguageID = translation.LanguageID;
                    _repoFunctionTranslation.Update(item);
                    await _repoFunctionSystem.SaveAll();
                }

                return new ResponseDetail<object> { Status = true };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }

        public async Task<ResponseDetail<object>> DeleteFunctionTranslation(int functionTranslationID)
        {
            try
            {
                var functionTranslation = await _repoFunctionTranslation.FindAll(x => x.ID == functionTranslationID)
                    .FirstOrDefaultAsync();
                _repoFunctionTranslation.Remove(functionTranslation);
                var result = await _repoFunctionTranslation.SaveAll();
                return new ResponseDetail<object> { Status = result };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }

        }

        public async Task<ResponseDetail<object>> DeleteFunction(int functionID)
        {

            try
            {
                var function = await _repoFunctionSystem.FindAll(x => x.ID == functionID)
              .Include(x => x.FunctionTranslations).FirstOrDefaultAsync();
                if (function.FunctionTranslations.Count > 0)
                    _repoFunctionTranslation.RemoveMultiple(function.FunctionTranslations.ToList());
                _repoFunctionSystem.Remove(function);
                var result = await _repoFunctionSystem.SaveAll();
                return new ResponseDetail<object> { Status = result };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }

        }

        public async Task<ResponseDetail<object>> AddFunction(FunctionDto functionDto)
        {
            try
            {
                var function = _mapper.Map<FunctionSystem>(functionDto);
                _repoFunctionSystem.Add(function);
                await _repoFunctionSystem.SaveAll();
                var list = new List<FunctionTranslation>();
                foreach (var translation in functionDto.Translations)
                {
                    var item = translation;
                    list.Add(new FunctionTranslation(function.ID, item.Name, item.LanguageID));
                }
                _repoFunctionTranslation.AddRange(list);
                await _repoFunctionSystem.SaveAll();
                return new ResponseDetail<object> { Status = true };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }
        public Task<ResponseDetail<object>> GetAllFunctionByPermision()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Action
        public async Task<object> GetScreenAction(int functionID)
        {
            var query = from a in _repoAction.FindAll()
                        join f in _repoActionInFunctionSystem.FindAll(x => x.FunctionSystemID == functionID)
                                    .Include(x => x.FunctionSystem)
                            on a.ID equals f.ActionID
                        into af
                        from c in af.DefaultIfEmpty()
                        select new
                        {
                            Id = a.ID,
                            Name = a.Name,
                            FuncName = c != null ? c.FunctionSystem.Name : "N/A",
                            Status = c != null ? true : false,
                        };
            var data = await query.ToListAsync();
            return data;
        }
        public async Task<ResponseDetail<object>> UpdateAction(Models.Action action)
        {
            _repoAction.Update(action);
            try
            {
                var result = await _repoAction.SaveAll();
                return new ResponseDetail<object> { Status = result };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }

        public async Task<ResponseDetail<object>> DeleteAction(int actionID)
        {
            var action = await _repoAction.FindAll(x => x.ID == actionID).FirstOrDefaultAsync();
            _repoAction.Remove(action);
            try
            {
                var result = await _repoAction.SaveAll();
                return new ResponseDetail<object> { Status = result };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }

        public async Task<ResponseDetail<object>> AddAction(Models.Action action)
        {
            _repoAction.Add(action);
            try
            {
                var result = await _repoAction.SaveAll();
                return new ResponseDetail<object> { Status = result };
            }
            catch (System.Exception ex)
            {
                return new ResponseDetail<object> { Message = ex.Message };
            }
        }

        public async Task<List<Models.Action>> GetAllAction() => await _repoAction.FindAll().ToListAsync();
        #endregion

        #region ActionInFuntion

        public async Task<object> GetScreenFunctionAndAction(ScreenFunctionAndActionRequest request)
        {

            var roleID = request.RoleIDs;
            var permission = _repoPermission.FindAll();
            var query = _repoActionInFunctionSystem.FindAll()
                .Include(x => x.Action)
                .Include(x => x.FunctionSystem)
                .ThenInclude(x => x.FunctionTranslations)
                .Include(x => x.FunctionSystem)
                .ThenInclude(x => x.Module)
                .Select(x => new
                {
                    Id = x.FunctionSystem.ID,
                    FunctionCode = x.FunctionSystem.Code,
                    Name = x.FunctionSystem.Name == "" || x.FunctionSystem.Name == null ? x.FunctionSystem.FunctionTranslations.First(x => x.LanguageID == "en").Name : x.FunctionSystem.Name,
                    ActionName = x.Action.Name,
                    ActionID = x.Action.ID,
                    SequenceFunction = x.FunctionSystem.Sequence,
                    Module = x.FunctionSystem.Module,
                    ModuleCode = x.FunctionSystem.Module.Code,
                    ModuleNameID = x.FunctionSystem.Module.ID,
                    Code = x.Action.Code,
                }).Where(x => !Permissions.Contains(x.FunctionCode)); 
            // Dieu kien nay de khong load nhung chuc nang he thong
            var model = from t1 in query
                        from t2 in permission.Where(x => roleID.Contains(x.RoleID) && t1.Id == x.FunctionSystemID && x.ActionID == t1.ActionID)
                            .DefaultIfEmpty()
                        select new
                        {
                            t1.Id,
                            t1.Name,
                            t1.ActionName,
                            t1.ActionID,
                            t1.Code,
                            t1.Module,
                            t1.SequenceFunction,
                            Permission = t2
                        };
            var data = (await model.ToListAsync())
                        .GroupBy(x => x.Module)
                        .Select(x => new
                        {
                            ModuleName = x.Key.Name,
                            Sequence = x.Key.Sequence,
                            Fields = new
                            {
                                DataSource = x.GroupBy(s => new { s.Id, s.Name, s.SequenceFunction })
                                .Select(g => new
                                {
                                    Id = g.Key.Id,
                                    Name = g.Key.Name,
                                    SequenceFunction = g.Key.SequenceFunction,
                                    Childrens = g
                                    .Select(a => new
                                    {
                                        ParentID = g.Key.Id,
                                        ID = $"{a.ActionID}_{g.Key.Id}_{roleID.FirstOrDefault()}",
                                        Name = a.ActionName,
                                        a.ActionID,
                                        FunctionID = g.Key.Id,
                                        a.ActionName,
                                        Status = a.Permission != null,
                                        // Status = permission.Any(p => roleID.Contains(p.RoleID) && a.ActionID == p.ActionID && p.FunctionSystemID == g.Key.Id) 
                                        // IsChecked = permission.Any(p => roleID.Contains(p.RoleID) && a.ActionID == p.ActionID && p.FunctionSystemID == g.Key.Id)

                                    }).ToList()
                                }).OrderBy(x => x.SequenceFunction).ToList(),
                                Id = "id",
                                Text = "name",
                                Child = "childrens"
                            }
                        });
            return data.OrderBy(x => x.Sequence).ToList();
        }


        public async Task<object> GetActionInFunctionByRoleID(int roleID)
        {
            var query = _repoPermission.FindAll(x => x.RoleID == roleID)
                .Include(x => x.Functions)
                .Include(x => x.Action)
                .Select(x => new
                {
                    x.Functions.Name,
                    FunctionCode = x.Functions.Code,
                    x.Functions.Url,
                    x.Action.Code,
                    x.ActionID
                });
            var data = (await query.ToListAsync()).GroupBy(x => new { x.Name, x.FunctionCode, x.Url })
                    .Select(x => new
                    {
                        x.Key.Name,
                        x.Key.FunctionCode,
                        x.Key.Url,
                        Childrens = x
                    });
            return data;
        }
        public async Task<ResponseDetail<object>> PostActionToFunction(int functionID, ActionAssignRequest request)
        {
            foreach (var actionId in request.ActionIds)
            {
                if (await _repoActionInFunctionSystem.FindAll(x => x.FunctionSystemID == functionID && x.ActionID == actionId).AnyAsync() != false)
                    return new ResponseDetail<object> { Status = false, Message = "This action has been existed in function" };
                var entity = new ActionInFunctionSystem
                {
                    ActionID = actionId,
                    FunctionSystemID = functionID
                };

                _repoActionInFunctionSystem.Add(entity);
            }

            try
            {
                var result = await _repoActionInFunctionSystem.SaveAll();
                return new ResponseDetail<object> { Status = true };
            }
            catch (System.Exception ex)
            {
                // TODO
                return new ResponseDetail<object> { Status = false, Message = ex.Message };
            }

            // tao role moi
        }
        public async Task<ResponseDetail<object>> DeleteActionToFunction(int functionID, ActionAssignRequest request)
        {
            try
            {
                foreach (var actionId in request.ActionIds)
                {
                    var entity = await _repoActionInFunctionSystem.FindAll(x => x.FunctionSystemID == functionID && x.ActionID == actionId).FirstOrDefaultAsync();
                    if (entity == null)
                        return new ResponseDetail<object> { Status = false, Message = "This action is not existed in function" };

                    _repoActionInFunctionSystem.Remove(entity);
                }
                var result = await _repoPermission.SaveAll();
                return new ResponseDetail<object> { Status = true };
            }
            catch (System.Exception ex)
            {
                // TODO
                return new ResponseDetail<object> { Status = false, Message = ex.Message };
            }

            // tao role moi
        }

        #endregion

        #region Permission

        public async Task<object> GetMenuByLangID(int userId, string langID)
        {
            var roles = await _repoUserRole.FindAll(x => x.UserID == userId).Select(x => x.RoleID).ToArrayAsync();

            var query = from p in _repoPermission.FindAll()
                        join f in _repoFunctionTranslation.FindAll(x => x.LanguageID.Equals(langID))
                                .Include(x => x.FunctionSystem)
                                .ThenInclude(x => x.Module)
                                .ThenInclude(x => x.ModuleTranslations)
                        on p.FunctionSystemID equals f.FunctionSystemID
                        join r in _repoRole.FindAll() on p.RoleID equals r.ID
                        join a in _repoAction.FindAll()
                            on p.ActionID equals a.ID
                        where roles.Contains(r.ID) && a.Code == "VIEW"
                        select new
                        {
                            Id = f.ID,
                            Name = f.Name,
                            Code = f.FunctionSystem.Code,
                            Url = f.FunctionSystem.Url,
                            Icon = f.FunctionSystem.Icon,
                            ParentId = f.FunctionSystem.ParentID,
                            SortOrder = f.FunctionSystem.Sequence,
                            Module = f.FunctionSystem.Module,
                            ModuleId = f.FunctionSystem.ModuleID
                        };
            var data = await query.Distinct()
                .OrderBy(x => x.ParentId)
                .ThenBy(x => x.SortOrder)
                .ToListAsync();
            return data.GroupBy(x => x.Module).Select(x => new
            {
                Module = x.Key.ModuleTranslations.Count > 0 ?
                x.Key.ModuleTranslations.FirstOrDefault(x => x.LanguageID.Equals(langID)).Name
                : x.Key.Name,
                Icon = x.Key.Icon,
                Url = x.Key.Url,
                Sequence = x.Key.Sequence,
                Children = x,
                HasChildren = x.Any()
            }).OrderBy(x => x.Sequence).ToList();
        }
        public async Task<ResponseDetail<object>> PutPermissionByRoleId(int roleID, UpdatePermissionRequest request)
        {

            try
            {
                //create new permission list from user changed
                var newPermissions = new List<Permission>();
                foreach (var p in request.Permissions)
                {
                    newPermissions.Add(new Permission(roleID, p.ActionID, p.FunctionID));
                }
                var existingPermissions =  _repoPermission.FindAll().Where(x => x.RoleID == roleID).ToList();
                if (existingPermissions.Count > 0)
                {
                    _repoPermission.RemoveMultiple(existingPermissions);

                }
                _repoPermission.AddRange(newPermissions.DistinctBy(x => new { x.RoleID, x.ActionID, x.FunctionSystemID }).ToList());

                await _repoPermission.SaveAll();
                return new ResponseDetail<object> { Status = true };
            }
            catch (System.Exception ex)
            {
                // TODO
                return new ResponseDetail<object> { Status = false, Message = ex.Message };
            }

            // tao role moi
        }


        public async Task<object> GetMenuByUserPermission(int userId)
        {
            var roles = await _repoUserRole.FindAll(x => x.UserID == userId).Select(x => x.RoleID).ToArrayAsync();
            var query = from f in _repoFunctionSystem.FindAll()
                                    .Include(x => x.Module)
                        join p in _repoPermission.FindAll()
                            on f.ID equals p.FunctionSystemID
                        join r in _repoRole.FindAll() on p.RoleID equals r.ID
                        join a in _repoAction.FindAll()
                            on p.ActionID equals a.ID
                        where roles.Contains(r.ID) && a.Code == "VIEW"
                        select new
                        {
                            Id = f.ID,
                            Name = f.Name,
                            Code = f.Code,
                            Url = f.Url,
                            Icon = f.Icon,
                            ParentId = f.ParentID,
                            SortOrder = f.Sequence,
                            Module = f.Module,
                            ModuleId = f.ModuleID
                        };
            var data = await query.Distinct()
                .OrderBy(x => x.ParentId)
                .ThenBy(x => x.SortOrder)
                .ToListAsync();
            return data.GroupBy(x => x.Module).Select(x => new
            {
                Module = x.Key.Name,
                Icon = x.Key.Icon,
                Url = x.Key.Url,
                Sequence = x.Key.Sequence,
                Children = x,
                HasChildren = x.Any()
            }).OrderBy(x => x.Sequence).ToList();
        }

        public Task<PagedList<Permission>> GetWithPaginations(PaginationParams param)
        {
            throw new NotImplementedException();
        }

        public Task<PagedList<Permission>> Search(PaginationParams param, object text)
        {
            throw new NotImplementedException();
        }

        public async Task<List<Permission>> GetAllAsync() => await _repoPermission.FindAll().ToListAsync();
        public async Task<bool> Add(Permission model)
        {
            var Permission = _mapper.Map<Permission>(model);

            _repoPermission.Add(Permission);
            return await _repoPermission.SaveAll();
        }

        public async Task<bool> Delete(object id)
        {
            var Permission = _repoPermission.FindById(id);
            _repoPermission.Remove(Permission);
            return await _repoPermission.SaveAll();
        }

        public async Task<bool> Update(Permission model)
        {
            var Permission = _mapper.Map<Permission>(model);
            _repoPermission.Update(Permission);
            return await _repoPermission.SaveAll();
        }


        public Permission GetById(object id) => _repoPermission.FindById(id);

        public async Task<List<FunctionSystem>> GetAllFunction()
        {
            var functions = await _repoFunctionSystem.FindAll().Include(x => x.Module).ToListAsync();
            return functions;
        }

        public async Task<List<Module>> GetAllModule()
        => await _repoModule.FindAll().OrderBy(x => x.Sequence).ToListAsync();
        #endregion

        #region Language
        public async Task<List<Language>> GetAllLanguage()
        {
            return await _repoLanguage.FindAll().ToListAsync();
        }


        #endregion

    }
}
