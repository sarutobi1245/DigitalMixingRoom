using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DMR_API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DMR_API._Repositories.Interface;
using DMR_API._Services.Interface;
using DMR_API.DTO;
using DMR_API.Models;
using Microsoft.EntityFrameworkCore;

namespace DMR_API._Services.Services
{
    public class PartService : IPartService
    {
        private readonly IPartRepository _repoLine;
        private readonly IMapper _mapper;
        private readonly MapperConfiguration _configMapper;
        public PartService(IPartRepository repoBrand, IMapper mapper, MapperConfiguration configMapper)
        {
            _configMapper = configMapper;
            _mapper = mapper;
            _repoLine = repoBrand;

        }

        //Thêm Brand mới vào bảng Line
        public async Task<bool> Add(PartDto model)
        {
            var Line = _mapper.Map<Part>(model);
            _repoLine.Add(Line);
            return await _repoLine.SaveAll();
        }

     

        //Lấy danh sách Brand và phân trang
        public async Task<PagedList<PartDto>> GetWithPaginations(PaginationParams param)
        {
            var lists = _repoLine.FindAll().ProjectTo<PartDto>(_configMapper).OrderByDescending(x => x.ID);
            return await PagedList<PartDto>.CreateAsync(lists, param.PageNumber, param.PageSize);
        }
      
       
        //Xóa Brand
        public async Task<bool> Delete(object id)
        {
            var Line = _repoLine.FindById(id);
            _repoLine.Remove(Line);
            return await _repoLine.SaveAll();
        }

        //Cập nhật Brand
        public async Task<bool> Update(PartDto model)
        {
            var Line = _mapper.Map<Part>(model);
            _repoLine.Update(Line);
            return await _repoLine.SaveAll();
        }
      
        //Lấy toàn bộ danh sách Brand 
        public async Task<List<PartDto>> GetAllAsync()
        {
            return await _repoLine.FindAll().ProjectTo<PartDto>(_configMapper).OrderByDescending(x => x.ID).ToListAsync();
        }

        //Lấy Brand theo Brand_Id
        public PartDto GetById(object id)
        {
            return  _mapper.Map<Part, PartDto>(_repoLine.FindById(id));
        }
        //Tìm kiếm Line
        public async Task<PagedList<PartDto>> Search(PaginationParams param, object text)
        {
            var lists = _repoLine.FindAll().ProjectTo<PartDto>(_configMapper)
            .Where(x => x.Name.Contains(text.ToString()))
            .OrderByDescending(x => x.ID);
            return await PagedList<PartDto>.CreateAsync(lists, param.PageNumber, param.PageSize);
        }
    }
}