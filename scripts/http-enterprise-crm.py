#!/usr/bin/env python3
"""Real MVC CRM journey in a disposable localhost enterprise fixture; never targets a remote service."""
from enterprise_http_fixture import Fixture, Forms
import re


def redirect(f, response, label, prefix):
    validation = re.search(r'<div[^>]*validation-summary-errors[^>]*>(.*?)</div>', response.text, re.S)
    detail = 'HTTP ' + str(response.status)
    if validation: detail += ': ' + re.sub(r'<[^>]+>', ' ', validation.group(1)).strip()
    f.check(label, response.status == 302 and response.location.startswith(prefix), detail)
    return response.location


def run():
    with Fixture('crm') as f:
        maker = f.login(1)
        reviewer = f.login(1, reviewer=True)
        outsider = f.login(2)
        company = f.scalar("SELECT Id FROM Companies WHERE Slug='demo-farm-1'")
        branch = f.scalar("SELECT Id FROM Branches WHERE CompanyId=? AND Name='Demo Branch 1'", (company,))
        product = f.scalar("SELECT Id FROM StockItems WHERE CompanyId=? AND BranchId=? AND UnitOfMeasure<>'' ORDER BY Id LIMIT 1", (company, branch))
        before = {table: f.scalar('SELECT COUNT(*) FROM '+table) for table in ['ProductSales', 'StockMovements', 'Vouchers', 'FinanceDocuments', 'Notifications']}
        for section in ['leads', 'opportunities', 'activities', 'quotes', 'sales-ready']:
            f.page(maker, '/crm?section='+section)
        new_lead = f.form(maker, '/crm/leads/new', '/crm/leads/save')
        denied = maker.request('/crm/leads/save', fields={'Reference':'HTTP-CSRF-DENIED','Name':'Fictional rejected lead'})
        f.check('Lead mutation rejects missing CSRF token', denied.status == 400)
        lead_path = redirect(f, maker.submit(new_lead, {'Reference':'HTTP-CRM-LEAD','Name':'Fictional HTTP buyer <script>alert(1)</script>','ContactName':'Synthetic contact','Email':'http-crm@example.invalid','Phone':'+000000000','Address':'Synthetic address','Source':'Disposable HTTP fixture','MarketingConsent':'Granted','ConsentEvidence':'FICTIONAL fixture consent only','ConsentReviewed':'true'}), 'Create lead with reviewed synthetic consent', '/crm/leads/')
        lead = int(lead_path.rsplit('/',1)[1])
        html = f.page(maker, lead_path).text
        f.check('Lead contact text is HTML encoded', '<script>alert(1)</script>' not in html and '&lt;script&gt;' in html)
        f.check('Consent event persisted with evidence', f.scalar('SELECT COUNT(*) FROM CRMConsentEvents WHERE LeadId=?',(lead,)) == 1)
        duplicate = maker.submit(new_lead, {'Reference':'http-crm-lead','Name':'Duplicate fixture'})
        f.check('Normalized duplicate lead blocked with validation', duplicate.status == 200 and f.scalar("SELECT COUNT(*) FROM CRMLeads WHERE Reference='HTTP-CRM-LEAD'") == 1)
        edit_path = lead_path+'/edit'
        stale_edit = f.form(maker, edit_path, '/crm/leads/save')
        updated = maker.submit(stale_edit, {'Notes':'Reviewed synthetic follow-up note'})
        redirect(f, updated, 'Edit lead with fresh concurrency version', '/crm/leads/')
        stale = maker.submit(stale_edit, {'Notes':'Stale attempt'})
        f.check('Stale lead edit blocked', stale.status == 200 and 'record changed' in stale.text and f.scalar('SELECT Notes FROM CRMLeads WHERE Id=?',(lead,)) == 'Reviewed synthetic follow-up note')
        opportunity_form = f.form(maker, '/crm/opportunities/new?leadId='+str(lead), '/crm/opportunities/save')
        opportunity_path = redirect(f, maker.submit(opportunity_form, {'Input.Reference':'HTTP-CRM-OPP','Input.Title':'Fictional feed purchase','Input.ReviewNote':'Synthetic qualification reviewed','Input.Reviewed':'true'}), 'Qualify reviewed opportunity', '/crm/opportunities/')
        opportunity = int(opportunity_path.rsplit('/',1)[1])
        activity_form = f.form(maker, '/crm/activities/new?leadId='+str(lead)+'&opportunityId='+str(opportunity), '/crm/activities/save')
        activity_path = redirect(f, maker.submit(activity_form, {'Input.Reference':'HTTP-CRM-TASK','Input.Title':'Review synthetic customer need','Input.Kind':'Task'}), 'Create dated internal activity', '/crm/activities/')
        close_form = f.form(maker, activity_path, activity_path+'/close')
        redirect(f, maker.submit(close_form, {'status':'Done','note':'Synthetic task manually completed'}), 'Complete internal activity', '/crm/activities/')
        repeated = maker.submit(close_form, {'status':'Done','note':'Repeat'})
        f.check('Repeated activity completion rejected', repeated.status == 200 and 'record changed' in repeated.text)
        followup_form = f.form(maker, '/crm/activities/new?leadId='+str(lead)+'&opportunityId='+str(opportunity), '/crm/activities/save')
        followup_path = redirect(f, maker.submit(followup_form, {'Input.Reference':'HTTP-CRM-FOLLOWUP','Input.Title':'Keep linked follow-up after opportunity closes','Input.Kind':'Task'}), 'Create open post-acceptance follow-up', '/crm/activities/')
        quote_form = f.form(maker, '/crm/quotes/new?opportunityId='+str(opportunity), '/crm/quotes/save')
        quote_path = redirect(f, maker.submit(quote_form, {'Input.Reference':'HTTP-CRM-QUOTE','Input.Terms':'FICTIONAL net-only estimate. Tax separately reviewed.','Input.Lines[0].StockItemId':product,'Input.Lines[0].Quantity':'2.5','Input.Lines[0].UnitPrice':'3.33','Input.Lines[0].DiscountAmount':'0.33','Input.Lines[1].StockItemId':product,'Input.Lines[1].Quantity':'1.125','Input.Lines[1].UnitPrice':'2','Input.Lines[1].DiscountAmount':'0'}), 'Create two-line net quote snapshot', '/crm/quotes/')
        quote = int(quote_path.rsplit('/',1)[1])
        f.check('Multi-line rounded quote amount persisted', abs(float(f.scalar('SELECT NetAmount FROM CRMQuotes WHERE Id=?',(quote,)))-10.25)<0.00001 and f.scalar('SELECT COUNT(*) FROM CRMQuoteLines WHERE QuoteId=?',(quote,))==2)
        review_form = f.form(reviewer, quote_path, quote_path+'/review')
        unreviewed = reviewer.submit(review_form, {'action':'Review','note':'Synthetic snapshot review','reviewed':'false'})
        f.check('Quote review requires explicit checkbox', unreviewed.status == 200 and f.scalar('SELECT Status FROM CRMQuotes WHERE Id=?',(quote,)) == 'Draft')
        redirect(f, reviewer.submit(review_form, {'action':'Review','note':'Synthetic recipient, units, currency and net-only scope reviewed','reviewed':'true'}), 'Review quote snapshot', '/crm/quotes/')
        accept_page = f.page(reviewer, quote_path)
        f.check('Acceptance effect and net-only tax boundaries disclosed', 'reserves no stock' in accept_page.text and 'creates no sale, invoice, tax posting' in accept_page.text and 'tax is not calculated here' in accept_page.text)
        acceptance = Forms(accept_page.text).get('/crm/quotes/accept')
        handoff_path = redirect(f, reviewer.submit(acceptance, {'Acceptance.ExistingCustomerId':'','Acceptance.NewCustomerCode':'HTTP-CRM-CUSTOMER','Acceptance.SalesReadyReference':'HTTP-CRM-READY','Acceptance.AcceptanceEvidence':'FICTIONAL customer acceptance solely for localhost verification','Acceptance.Reviewed':'true'}), 'Accept reviewed quote into customer and internal handoff', '/crm/sales-ready/')
        f.page(reviewer, handoff_path)
        f.check('Customer and handoff created once with correct scope', f.scalar("SELECT COUNT(*) FROM TradingParties WHERE CompanyId=? AND BranchId=? AND Code='HTTP-CRM-CUSTOMER'",(company,branch))==1 and f.scalar('SELECT COUNT(*) FROM CRMSalesReadyRecords WHERE QuoteId=?',(quote,))==1)
        f.check('Opportunity is won and lead linked', f.scalar('SELECT Stage FROM CRMOpportunities WHERE Id=?',(opportunity,))=='Won' and f.scalar('SELECT CustomerId FROM CRMLeads WHERE Id=?',(lead,)) is not None)
        followup_edit = f.form(maker, followup_path+'/edit', '/crm/activities/save')
        redirect(f, maker.submit(followup_edit, {'Input.Title':'Updated linked follow-up after acceptance'}), 'Edit open task retaining now-won opportunity', '/crm/activities/')
        f.check('Closed opportunity task link is retained', f.scalar('SELECT OpportunityId FROM CRMActivities WHERE Id=?',(int(followup_path.rsplit('/',1)[1]),))==opportunity)
        replay = reviewer.submit(acceptance, {'Acceptance.NewCustomerCode':'HTTP-CRM-CUSTOMER','Acceptance.SalesReadyReference':'HTTP-CRM-READY','Acceptance.AcceptanceEvidence':'FICTIONAL replay','Acceptance.Reviewed':'true'})
        f.check('Repeated acceptance cannot duplicate customer or handoff', replay.status==200 and f.scalar('SELECT COUNT(*) FROM CRMSalesReadyRecords WHERE QuoteId=?',(quote,))==1 and f.scalar("SELECT COUNT(*) FROM TradingParties WHERE Code='HTTP-CRM-CUSTOMER'")==1)
        for path in [lead_path, opportunity_path, activity_path, quote_path, handoff_path]:
            f.check('Cross-company record access denied '+path, outsider.request(path).status==404)
        store = f.scalar("SELECT Id FROM Branches WHERE CompanyId=? AND Name='Demo Store Branch 1'", (company,))
        other_branch = f.login(1, branch=store)
        for path in [lead_path, opportunity_path, activity_path, quote_path, handoff_path]:
            f.check('Cross-branch record access denied '+path, other_branch.request(path).status==404)
        after = {table: f.scalar('SELECT COUNT(*) FROM '+table) for table in before}
        f.check('CRM journey made no stock, financial or outbound-notification changes', before==after)
        f.page(maker, '/crm?section=leads&q=HTTP-CRM&status=Converted')
        f.page(maker, '/crm?section=activities&status=Done')
        f.page(maker, '/crm?section=sales-ready&q=HTTP-CRM')
        f.report['scope']='Real HTTP forms, Identity, CSRF, consent, stale edits, opportunities, activities, rounded multi-line quote, reviewer conversion, duplicate replay, tenant/branch isolation, no automatic posting. Browser JavaScript and visual layout are not claimed.'

if __name__ == '__main__':
    run()
