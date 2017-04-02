using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;

namespace LyncSDKExtensions
{
    internal static class ParticipantExtensions
    {
        public static bool IsAvailable(this Participant participant)
        {
            var availibity = (ContactAvailability)participant.Contact.GetContactInformation(ContactInformationType.Availability);
            return (availibity != ContactAvailability.None &&
                    availibity != ContactAvailability.Offline &&
                    availibity != ContactAvailability.Invalid);
        }
    }
}
