"""Invoice/approval pagination checks for http-smoke.py's disposable localhost fixture.

No standalone entry point, external host, production database, or credentials are accepted.
Temporary billing authority belongs only to the existing synthetic administrator and is
removed in finally. Existing users and their grants are compared before returning.
"""
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import sqlite3
from urllib.parse import parse_qs, urlencode, urlsplit


class BrowseMarkup(HTMLParser):
    def __init__(self, document):
        super().__init__(convert_charrefs=True)
        self.links, self.sections, self.stack = [], {}, []
        self.in_body = False
        self.feed(document)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'a' and attrs.get('href'):
            self.links.append(attrs['href'])
        if tag == 'section':
            self.stack.append(attrs.get('id', ''))
        if tag == 'tbody':
            self.in_body = True
        if tag == 'tr' and self.in_body:
            for section in self.stack:
                if section:
                    self.sections[section] = self.sections.get(section, 0) + 1

    def handle_endtag(self, tag):
        if tag == 'section' and self.stack:
            self.stack.pop()
        if tag == 'tbody':
            self.in_body = False


def run_admin_paging_checks(dbpath, admin, Forms, check, page, *, company, branch, other_company):
    dbpath = Path(dbpath).resolve()
    if not dbpath.is_file() or not dbpath.parent.name.startswith('farm-http-demo-') or dbpath.name != 'farm-demo.db':
        raise ValueError('Invoice/approval smoke fixtures require the disposable farm-http-demo database')
    base = urlsplit(admin.base)
    if base.scheme != 'http' or base.hostname not in ('127.0.0.1', 'localhost', '::1'):
        raise ValueError('Invoice/approval smoke checks require a localhost HTTP session')

    def connect():
        db = sqlite3.connect(dbpath)
        db.execute('PRAGMA foreign_keys=ON')
        return db

    def scalar(query, args=()):
        with connect() as db:
            return db.execute(query, args).fetchone()[0]

    def filtered(form, **overrides):
        if form['method'] != 'get':
            raise AssertionError('Expected the rendered GET search form')
        fields = dict(form['fields'], **overrides)
        return urlsplit(form['action']).path + '?' + urlencode(fields)

    def next_link(response, field):
        matches = [link for link in BrowseMarkup(response.text).links
                   if parse_qs(urlsplit(link).query).get(field) == ['2']]
        check('Rendered ' + field + ' next-page link exists', len(matches) == 1)
        return matches[0]

    def options(response, action, field):
        return [int(item['value']) for item in Forms(response.text).get(action)['options'][field] if item['value']]

    def refresh_context(label):
        response = page(admin, '/Context', label + ' context form')
        response = admin.submit(Forms(response.text).get('/Context/Select'), {'companyId': company, 'branchId': branch})
        check(label, response.status == 302 and response.location == '/', 'HTTP ' + str(response.status))

    def account_snapshot(db, user_ids):
        placeholders = ','.join('?' for _ in user_ids)
        tables = ('AspNetUsers', 'AspNetUserClaims', 'AspNetUserRoles', 'UserPermissions', 'UserCompanyAccess', 'UserBranchAccess')
        return {table: sorted(db.execute('SELECT * FROM "' + table + '" WHERE "' +
                    ('Id' if table == 'AspNetUsers' else 'UserId') + '" IN (' + placeholders + ')', user_ids).fetchall(), key=repr)
                for table in tables}

    now = datetime.now(timezone.utc)
    ticks = (now.date().toordinal() - 1) * 864000000000 + (now.hour * 3600 + now.minute * 60 + now.second) * 10000000 + now.microsecond * 10
    old_reference = 'HTTP-PAGE-INVOICE-OLD'
    invoice_prefix = 'HTTP-PAGE-INVOICE-'
    requester = 'http-paging-independent-requester'
    payload = 'Review synthetic older proposal without executing a transaction'
    inserted_claim = inserted_permission = None

    with connect() as db:
        admin_id = db.execute("SELECT Id FROM AspNetUsers WHERE Email='farmdemo1@example.invalid'").fetchone()[0]
        user_ids = tuple(row[0] for row in db.execute('SELECT Id FROM AspNetUsers ORDER BY Id'))
        accounts_before = account_snapshot(db, user_ids)
        vouchers_before = db.execute('SELECT COUNT(*) FROM Vouchers').fetchone()[0]
        executions_before = db.execute('SELECT COUNT(*) FROM AutomationExecutions').fetchone()[0]
        stock_before = db.execute('SELECT Id, QuantityOnHand FROM StockItems ORDER BY Id').fetchall()

        def invoice(tenant, reference, due, paid):
            invoice_id = db.execute('INSERT INTO SubscriptionInvoices (CompanyId, Reference, DueDate, Amount, Paid, Cancelled) VALUES (?, ?, ?, ?, ?, 0)',
                                    (tenant, reference, due.isoformat(), '100.0', str(paid))).lastrowid
            if paid:
                db.execute('INSERT INTO SubscriptionPayments (CompanyId, InvoiceId, Reference, Amount, RecordedUtc) VALUES (?, ?, ?, ?, ?)',
                           (tenant, invoice_id, reference + '-RECEIPT', str(paid), ticks))
            return invoice_id

        old_invoice = invoice(company, old_reference, now.date() - timedelta(days=90), Decimal('0.0'))
        for number in range(100):
            invoice(company, invoice_prefix + f'RECENT-{number:03}', now.date(), Decimal('100.0'))
        other_invoice = invoice(other_company, old_reference, now.date(), Decimal('100.0'))

        db.execute('''INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                      EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed,
                      TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                      VALUES (?, ?, ?, ?, ?, 0, NULL, ?, ?, 0, 0, 1, 0)''',
                   (requester, requester + '@example.invalid', (requester + '@example.invalid').upper(),
                    requester + '@example.invalid', (requester + '@example.invalid').upper(), requester, requester))
        db.execute('INSERT INTO UserCompanyAccess (UserId, CompanyId) VALUES (?, ?)', (requester, company))
        db.execute('INSERT INTO UserBranchAccess (UserId, CompanyId, BranchId) VALUES (?, ?, ?)', (requester, company, branch))
        db.execute('INSERT INTO UserPermissions (UserId, CompanyId, Permission) VALUES (?, ?, ?)', (requester, company, 'Automation.Request'))
        rule_id = db.execute('INSERT INTO AutomationRules (CompanyId, Name, Trigger, Action, Active) VALUES (?, ?, ?, ?, 1)',
                             (company, 'HTTP paging review only', 'Manual', 'Review')).lastrowid
        other_rule = db.execute('INSERT INTO AutomationRules (CompanyId, Name, Trigger, Action, Active) VALUES (?, ?, ?, ?, 1)',
                                (other_company, 'HTTP other-company review', 'Manual', 'Review')).lastrowid

        def approval(tenant, rule, actor, proposal):
            bound = json.dumps({'CompanyId': tenant, 'Id': rule, 'Trigger': 'Manual', 'Action': 'Review', 'Payload': proposal}, separators=(',', ':'))
            digest = hashlib.sha256(bound.encode()).hexdigest().upper()
            return db.execute('''INSERT INTO AutomationApprovals (CompanyId, RuleId, RequesterId, ApproverId, Status, PayloadHash, CreatedUtc)
                                 VALUES (?, ?, ?, NULL, 'Pending', ?, ?)''', (tenant, rule, actor, digest, ticks)).lastrowid

        old_approval = approval(company, rule_id, requester, payload)
        for number in range(50):
            approval(company, rule_id, admin_id, 'Synthetic newer request ' + str(number))
        other_actor = db.execute('SELECT UserId FROM UserCompanyAccess WHERE CompanyId=? ORDER BY Id LIMIT 1', (other_company,)).fetchone()[0]
        other_approval = approval(other_company, other_rule, other_actor, 'Synthetic other-company request')

    try:
        response = page(admin, '/Subscription', 'Older invoice keeps billing reachable during suspension')
        check('Older debt still suspends while invoice history defaults to a bounded recent page',
              'data-i18n-value="Suspended"' in response.text and BrowseMarkup(response.text).sections.get('invoice-browse') == 100)
        check('Ordinary tenant admin cannot see a subscription payment form',
              not any(form['action'] == '/Subscription/payment' for form in Forms(response.text).forms))
        csrf = Forms(response.text).get('/Account/Logout')['fields']['__RequestVerificationToken']
        denied = admin.request('/Subscription/payment', fields={'__RequestVerificationToken': csrf, 'invoiceId': old_invoice,
                                                                'reference': 'HTTP-PAGING-DENIED', 'amount': '100'})
        check('Older invoice cannot be paid without platform billing authority', denied.status == 403)
        with connect() as db:
            if not db.execute('SELECT 1 FROM UserPermissions WHERE CompanyId=? AND UserId=? AND Permission=?', (company, admin_id, 'Subscription.Pay')).fetchone():
                inserted_permission = db.execute('INSERT INTO UserPermissions (CompanyId, UserId, Permission) VALUES (?, ?, ?)',
                                                 (company, admin_id, 'Subscription.Pay')).lastrowid
            if not db.execute('SELECT 1 FROM AspNetUserClaims WHERE UserId=? AND ClaimType=? AND ClaimValue=?', (admin_id, 'permission', 'platform.billing')).fetchone():
                inserted_claim = db.execute('INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue) VALUES (?, ?, ?)',
                                           (admin_id, 'permission', 'platform.billing')).lastrowid
        refresh_context('Refresh only synthetic admin with temporary billing authority')
        response = page(admin, '/Subscription', 'Authorized billing recent-history selector')
        check('Oldest unpaid invoice is outside the initial100 selector', old_invoice not in options(response, '/Subscription/payment', 'invoiceId'))
        form = Forms(response.text).get('/Subscription#invoice-browse')
        response = page(admin, filtered(form, invoiceSearch=invoice_prefix, invoiceStatus='All'), 'Rendered invoice search form')
        link = next_link(response, 'invoicePage')
        check('Invoice pager preserves search and status', parse_qs(urlsplit(link).query).get('invoiceSearch') == [invoice_prefix]
              and parse_qs(urlsplit(link).query).get('invoiceStatus') == ['All'])
        response = page(admin, link, 'Oldest101st invoice page through rendered next link')
        check('Oldest101st invoice is selectable with no other-company invoice',
              options(response, '/Subscription/payment', 'invoiceId') == [old_invoice]
              and BrowseMarkup(response.text).sections.get('invoice-browse') == 1 and other_invoice != old_invoice)
        response = page(admin, '/Subscription?' + urlencode({'invoiceSearch': invoice_prefix, 'invoicePage': 2147483647}), 'Invoice page clamps excessive index')
        check('Clamped last invoice page stays bounded and actionable', options(response, '/Subscription/payment', 'invoiceId') == [old_invoice]
              and BrowseMarkup(response.text).sections.get('invoice-browse') == 1)
        form = Forms(response.text).get('/Subscription#invoice-browse')
        response = page(admin, filtered(form, invoiceSearch=old_reference, invoiceStatus='Open'), 'Search oldest open invoice')
        check('Open invoice filter exposes exactly the tenant old debt', options(response, '/Subscription/payment', 'invoiceId') == [old_invoice]
              and BrowseMarkup(response.text).sections.get('invoice-browse') == 1)
        payment = admin.submit(Forms(response.text).get('/Subscription/payment'), {'invoiceId': old_invoice, 'reference': 'HTTP-PAGING-RECEIVED', 'amount': '100'})
        check('Actual oldest invoice form records previously received payment', payment.status == 302 and payment.location == '/Subscription')
        check('Oldest invoice payment allocates100 once',
              Decimal(str(scalar('SELECT Paid FROM SubscriptionInvoices WHERE Id=?', (old_invoice,)))) == Decimal('100')
              and scalar("SELECT COUNT(*) FROM SubscriptionPayments WHERE InvoiceId=? AND Reference='HTTP-PAGING-RECEIVED'", (old_invoice,)) == 1)
        response = page(admin, '/Subscription', 'Subscription recovers after oldest debt settlement')
        check('Oldest payment clears suspension', 'data-i18n-value="Current"' in response.text)

        response = page(admin, '/Automation', 'Recent approval history stays bounded')
        check('Initial50 approval selector omits older pending and other-company records',
              len(options(response, '/Automation/approve', 'approvalId')) == 50
              and old_approval not in options(response, '/Automation/approve', 'approvalId')
              and other_approval not in options(response, '/Automation/approve', 'approvalId')
              and BrowseMarkup(response.text).sections.get('approval-browse') == 50)
        form = Forms(response.text).get('/Automation#approval-browse')
        response = page(admin, filtered(form, approvalSearch='', approvalStatus='Pending'), 'Rendered pending approval filter')
        link = next_link(response, 'approvalPage')
        check('Approval pager preserves pending filter', parse_qs(urlsplit(link).query).get('approvalStatus') == ['Pending'])
        response = page(admin, link, 'Oldest51st pending approval through rendered next link')
        check('Oldest51st pending proposal is selectable', options(response, '/Automation/approve', 'approvalId') == [old_approval]
              and BrowseMarkup(response.text).sections.get('approval-browse') == 1)
        response = page(admin, '/Automation?' + urlencode({'approvalStatus': 'Pending', 'approvalPage': 2147483647}), 'Approval page clamps excessive index')
        check('Clamped last approval page retains oldest pending selector', options(response, '/Automation/approve', 'approvalId') == [old_approval])
        form = Forms(response.text).get('/Automation#approval-browse')
        response = page(admin, filtered(form, approvalSearch=requester, approvalStatus='Pending'), 'Search oldest independent requester')
        check('Requester filter exposes one same-tenant proposal', options(response, '/Automation/approve', 'approvalId') == [old_approval])
        approval_form = Forms(response.text).get('/Automation/approve')
        changed = admin.submit(approval_form, {'approvalId': old_approval, 'payload': payload + ' changed'})
        check('Oldest proposal still requires unchanged hash-bound payload', changed.status == 409
              and scalar('SELECT Status FROM AutomationApprovals WHERE Id=?', (old_approval,)) == 'Pending')
        approved = admin.submit(approval_form, {'approvalId': old_approval, 'payload': payload})
        check('Actual oldest pending proposal accepts independent unchanged approval', approved.status == 302 and approved.location == '/Automation')
        with connect() as db:
            check('Older approval is audit-only and attributed to independent admin',
                  db.execute('SELECT Status, ApproverId FROM AutomationApprovals WHERE Id=?', (old_approval,)).fetchone() == ('Approved', admin_id)
                  and db.execute('SELECT COUNT(*) FROM AutomationExecutions').fetchone()[0] == executions_before
                  and db.execute('SELECT COUNT(*) FROM Vouchers').fetchone()[0] == vouchers_before
                  and db.execute('SELECT Id, QuantityOnHand FROM StockItems ORDER BY Id').fetchall() == stock_before)
            check('HTTP paging fixture respects relational tenant foreign keys', not db.execute('PRAGMA foreign_key_check').fetchall())
            invoice_rows = db.execute('SELECT Id, Paid FROM SubscriptionInvoices WHERE Reference LIKE ?', (invoice_prefix + '%',)).fetchall()
            check('Synthetic paid invoice balances equal recorded payment allocations', all(
                  Decimal(str(paid)) == sum((Decimal(str(row[0])) for row in db.execute('SELECT Amount FROM SubscriptionPayments WHERE InvoiceId=?', (invoice_id,))), Decimal(0))
                  for invoice_id, paid in invoice_rows))
        response = page(admin, '/Automation?' + urlencode({'approvalSearch': requester, 'approvalStatus': 'Approved'}), 'Approved status filter after older review')
        check('Approved history cannot offer already-approved proposal again',
              not options(response, '/Automation/approve', 'approvalId') and BrowseMarkup(response.text).sections.get('approval-browse') == 1)
        response = page(admin, '/Automation?' + urlencode({'approvalSearch': other_actor, 'approvalStatus': 'Pending'}), 'Other-company requester search')
        check('Other-company approval is absent from filtered history and selector',
              not options(response, '/Automation/approve', 'approvalId') and BrowseMarkup(response.text).sections.get('approval-browse', 0) == 0)
    finally:
        with connect() as db:
            # If an assertion interrupted settlement, retire only this helper's unpaid
            # synthetic debt so cleanup cannot strand the remaining fixture in suspension.
            balance = db.execute('SELECT Amount, Paid FROM SubscriptionInvoices WHERE Id=? AND CompanyId=?', (old_invoice, company)).fetchone()
            if balance and Decimal(str(balance[1])) < Decimal(str(balance[0])):
                db.execute('UPDATE SubscriptionInvoices SET Cancelled=1 WHERE Id=? AND CompanyId=?', (old_invoice, company))
            if inserted_claim is not None:
                db.execute('DELETE FROM AspNetUserClaims WHERE Id=? AND UserId=?', (inserted_claim, admin_id))
            if inserted_permission is not None:
                db.execute('DELETE FROM UserPermissions WHERE Id=? AND UserId=? AND CompanyId=?', (inserted_permission, admin_id, company))
            check('HTTP paging restores all existing accounts and grants exactly', account_snapshot(db, user_ids) == accounts_before)
        refresh_context('Remove temporary synthetic billing authority from cookie')
        response = page(admin, '/Subscription', 'Ordinary billing visibility restored after paging test')
        check('Temporary platform billing form is removed', not any(form['action'] == '/Subscription/payment' for form in Forms(response.text).forms))
        check('Main synthetic company is not left suspended by paging checks', 'data-i18n-value="Suspended"' not in response.text)
