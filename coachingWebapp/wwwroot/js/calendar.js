window.initializeCalendar = (dotNetHelper, slots, busyTimes, firstAvailableISO) => {
    const calendarElement = document.getElementById("calendar");

    if (calendarElement.calendar) {
        calendarElement.calendar.destroy();
    }

    const initialDate = firstAvailableISO ? new Date(firstAvailableISO) : new Date();

    const calendar = new FullCalendar.Calendar(calendarElement, {
        initialView: window.innerWidth <= 1024 ? 'listWeek' : 'timeGridWeek',
        initialDate: initialDate,
        timeZone: 'local',
        dayMaxEvents: true,
        nowIndicator: true,
        allDaySlot: false,
        selectable: true,
        eventOverlap: false,
        slotMinTime: '10:00:00',
        slotMaxTime: '21:15:00',
        scrollTime: '10:00:00',
        slotDuration: '00:45:00',
        slotLabelInterval: '00:45:00',
        height: 'auto', 
        aspectRatio: window.innerWidth <= 768 ? 0.45 : 1.1,
        validRange: {
            start: new Date().toISOString(),
            end: new Date(new Date().setDate(new Date().getDate() + 25)).toISOString()
        },
        eventOrder: 'order',
        eventDisplay: 'block',
        events: [
            ...busyTimes.map(busy => ({
                start: busy.startDateTimeOffset,
                end: busy.endDateTimeOffset,
                title: '⛔ Busy',
                backgroundColor: '#f8d7da',
                textColor: '#842029',
                className: 'fc-event-busy',
                order: 1
            })),
            ...slots.map(slot => ({
                start: slot,
                end: new Date(new Date(slot).getTime() + 45 * 60 * 1000),
                title: '🟢 Available',
                backgroundColor: '#d1e7dd',
                textColor: '#0f5132',
                className: 'fc-event-available',
                order: 2
            })),
        ],
        eventClick: function (info) {
            info.jsEvent.preventDefault();
            if (info.event.title.includes("Available")) {
                dotNetHelper.invokeMethodAsync("SelectTimeSlotFromJS", info.event.startStr);
            }
        }
    });

    calendar.render();
    calendarElement.calendar = calendar;

    const userTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    dotNetHelper.invokeMethodAsync("SetUserTimeZone", userTimeZone)
        .then(() => console.log("SetUserTimeZone invoked successfully"))
        .catch(err => console.error("Error invoking SetUserTimeZone:", err));

};