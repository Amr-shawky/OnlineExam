using MediatR;
using OnlineExam.Domain;
using OnlineExam.Domain.Interfaces;
using OnlineExam.Features.Categories.Dtos;
using OnlineExam.Features.Categories.Queries;
using OnlineExam.Shared.Responses;

namespace OnlineExam.Features.Categories.Handlers
{
    public class GetUserCategoriesQueryHandler : IRequestHandler<GetUserCategoriesQuery, ServiceResponse<PagedResult<UserCategoryDto>>>
    {
        private readonly IGenericRepository<Category> _categoryRepository;

        public GetUserCategoriesQueryHandler(IGenericRepository<Category> categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<ServiceResponse<PagedResult<UserCategoryDto>>> Handle(GetUserCategoriesQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var categories = _categoryRepository.GetAll().Where(category => category.IsDeleted==false);

                // Get total count before pagination
                var totalCount = categories.Count();

                // Apply pagination
                var paginatedCategories = categories
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(c => new UserCategoryDto
                    {
                        Title = c.Title,
                        IconUrl = c.IconUrl
                    })
                    .ToList();

                // Create paged result
                var pagedResult = new PagedResult<UserCategoryDto>(
                    paginatedCategories,
                    totalCount,
                    request.PageNumber,
                    request.PageSize
                );

                return ServiceResponse<PagedResult<UserCategoryDto>>.SuccessResponse(
                    pagedResult,
                    "Categories retrieved successfully",
                    "تم استرجاع الفئات بنجاح"
                );
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging configured
                return ServiceResponse<PagedResult<UserCategoryDto>>.InternalServerErrorResponse(
                    "An error occurred while retrieving categories",
                    "حدث خطأ أثناء استرجاع الفئات"
                );
            }
        }
    }
}
