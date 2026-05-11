using MediatR;
using OnlineExam.Domain;
using OnlineExam.Domain.Interfaces;
using OnlineExam.Features.Categories.Dtos;
using OnlineExam.Features.Categories.Queries;
using OnlineExam.Shared.Responses;

namespace OnlineExam.Features.Categories.Handlers
{
    public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, ServiceResponse<CategoryDetailsDto>>
    {
        private readonly IGenericRepository<Category> _categoryRepository;

        public GetCategoryByIdQueryHandler(IGenericRepository<Category> categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<ServiceResponse<CategoryDetailsDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryRepository.GetByIdAsync(request.Id);
                if (category == null || category.IsDeleted)
                {
                    return ServiceResponse<CategoryDetailsDto>.NotFoundResponse(
                        "Category not found",
                        "الفئة غير موجودة"
                    );
                }

                var categoryDto = new CategoryDetailsDto
                {
                    Id = category.Id,
                    Title = category.Title,
                    IconUrl = category.IconUrl,
                    CreationDate = category.CreatedAt
                };

                return ServiceResponse<CategoryDetailsDto>.SuccessResponse(
                    categoryDto,
                    "Category retrieved successfully",
                    "تم استرجاع الفئة بنجاح"
                );
            }
            catch (Exception ex)
            {
                return ServiceResponse<CategoryDetailsDto>.InternalServerErrorResponse(
                    "An error occurred while retrieving category",
                    "حدث خطأ أثناء استرجاع الفئة"
                );
            }
        }
    }
}
