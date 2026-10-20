/** Individual feature candidate: filter only the rows already loaded in the browser.
 * No fetch, navigation, or database search is performed while typing.
 */
class TicketFilter {
    constructor(input, table, feedback) {
        this.input = input;
        this.rows = [...table.querySelectorAll('tbody tr')];
        this.feedback = feedback;
        input.addEventListener('input', () => this.apply());
    }
    apply() {
        const words = this.input.value.trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);
        let visible = 0;
        for (const row of this.rows) {
            const text = row.textContent.toLocaleLowerCase();
            row.hidden = !words.every(word => text.includes(word));
            if (!row.hidden) visible++;
        }
        this.feedback.textContent = `${visible} of ${this.rows.length} loaded tickets shown`;
    }
}
const filterInput = document.querySelector('#loaded-filter');
if (filterInput) new TicketFilter(filterInput, document.querySelector('#ticket-table'), document.querySelector('#filter-feedback'));
