using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Karamem0.BookingServiceBot.Models
{

    public class BookingProfile
    {

        public string BookingBusinessId { get; set; }

        public string BookingBusinessName { get; set; }

        public string BookingStaffMemberId { get; set; }

        public string BookingServiceId { get; set; }

        public string BookingServiceName { get; set; }

        public DateTime BookingStartTime { get; set; }

        public DateTime BookingEndTime { get; set; }

        public string BookingCustomerId { get; set; }

        public string BookingCustomerName { get; set; }

        public string BookingCustomerEmail { get; set; }

    }

}
