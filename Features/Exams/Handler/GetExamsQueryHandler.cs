using MediatR;
using OnlineExam.Domain;
using OnlineExam.Domain.Interfaces;
using OnlineExam.Features.Exams.Dtos;
using OnlineExam.Features.Exams.Queries;
using OnlineExam.Shared.Responses;

namespace OnlineExam.Features.Exams.Handlers
{
    public class GetUserExamsQueryHandler : IRequestHandler<GetExamsQuery, ServiceResponse<PagedResult<UserExamDto>>>
    {
        private readonly IGenericRepository<Exam> _examRepository;

        public GetUserExamsQueryHandler(IGenericRepository<Exam> examRepository)
        {
            _examRepository = examRepository;
        }

        public async Task<ServiceResponse<PagedResult<UserExamDto>>> Handle(GetExamsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var exams = _examRepository.GetAll()
                    .Where(exam => exam.IsActive &&
                           !exam.IsDeleted &&
                           exam.StartDate <= DateTime.UtcNow &&
                           exam.EndDate >= DateTime.UtcNow);

                // Filter by category if provided
                if (request.CategoryId.HasValue)
                {
                    exams = exams.Where(e => e.CategoryId == request.CategoryId.Value);
                }

                // Get total count before pagination
                var totalCount = exams.Count();

                // Apply pagination
                var paginatedExams = exams
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(e => new UserExamDto
                    {
                        Id = e.Id,
                        Title = e.Title,
                        IconUrl = e.IconUrl,
                        CategoryName = e.Category!.Title,
                        StartDate = e.StartDate,
                        EndDate = e.EndDate,
                        Duration = e.Duration,
                        Description = e.Description
                    })
                    .ToList();

                // Create paged result
                var pagedResult = new PagedResult<UserExamDto>(
                    paginatedExams,
                    totalCount,
                    request.PageNumber,
                    request.PageSize
                );

                return ServiceResponse<PagedResult<UserExamDto>>.SuccessResponse(
                    pagedResult,
                    "Exams retrieved successfully",
                    "تم استرجاع الامتحانات بنجاح"
                );
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging configured
                return ServiceResponse<PagedResult<UserExamDto>>.InternalServerErrorResponse(
                    "An error occurred while retrieving exams",
                    "حدث خطأ أثناء استرجاع الامتحانات"
                );
            }
        }
    }
}
