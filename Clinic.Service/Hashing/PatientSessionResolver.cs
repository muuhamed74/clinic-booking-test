using Clinic.Domain.Entities.Patient_Notifications;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs;
using Service_Abstraction;

namespace Clinic_booking_site.Helpers.Hashing
{
    public class PatientSessionResolver : IPatientSessionResolver
    {
        private readonly IUnitOfWork _unitOfWork;
        public PatientSessionResolver(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<Patient?> ResolveAsync(string? rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return null;

            var hash = TokenHasher.Hash(rawToken);
            var session = await _unitOfWork.Reposit<PatientSession>()
                .GetEntityWithSpec(new ActiveSessionByTokenHashSpecification(hash));

            if (session == null)
                return null;

            session.LastSeenAt = DateTime.UtcNow;
            _unitOfWork.Reposit<PatientSession>().Update(session);
            await _unitOfWork.CompleteAsync();

            return await _unitOfWork.Reposit<Patient>().GetByIdAsync(session.PatientId);
        }
    }
}
