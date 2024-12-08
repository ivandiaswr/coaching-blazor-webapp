window.initializeCalendar = (dotNetHelper, slots, busyTimes) => {
    const calendarEl = document.getElementById("calendar");

    if (calendarEl.calendar) {
        calendarEl.calendar.destroy();
    }

    const calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: window.innerWidth <= 1024 ? 'listWeek' : 'timeGridWeek',
        initialDate: new Date(), 
        nowIndicator: true,
        selectable: true,
        eventOverlap: false,
        timeZone: 'local',
        validRange: {
            start: new Date().toISOString(),
            end: new Date(new Date().setDate(new Date().getDate() + 13)).toISOString()
        },
        scrollTime: '10:00:00',
        slotDuration: '00:30:00',
        slotLabelInterval: '00:30:00',
        events: [
            ...busyTimes.map(busy => ({
                start: busy.startDateTimeOffset,
                end: busy.endDateTimeOffset,
                title: 'Busy', 
                className: 'fc-event-busy',
                backgroundColor: '#f8d7da',
            })),
            ...slots.map(slot => ({
                start: slot,
                end: new Date(new Date(slot).getTime() + 30 * 60 * 1000), // 30-minute slot
                title: 'Open Slot', 
                backgroundColor: '#a8d5a2', 
                className: 'fc-event-available',
            })),
        ],
        eventClick: function (info) {
            info.jsEvent.preventDefault();
        
            if (info.event.title === "Open Slot") {
                dotNetHelper.invokeMethodAsync("SelectTimeSlotFromJS", info.event.startStr);
            }
        },
    });

    calendar.render();

    calendarEl.calendar = calendar;
};
