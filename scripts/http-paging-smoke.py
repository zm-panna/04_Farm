#!/usr/bin/env python3
"""Older actionable-record acceptance inside http-smoke.py's disposable localhost fixture.

This module has no CLI, URL, credential, or arbitrary database entry point. The main
harness passes its existing temporary database, authenticated session and check
functions. Fixture writes preserve foreign keys, balanced vouchers, stock movements
and carrying values; actual old-record actions are posted through rendered MVC forms.
"""
from datetime import datetime, timezone
from decimal import Decimal
from html.parser import HTMLParser
from html import unescape
from pathlib import Path
import re
import runpy
import sqlite3
import urllib.parse


class Links(HTMLParser):
    def __init__(self, text):
        super().__init__(convert_charrefs=True)
        self.hrefs = []
        self.feed(text)

    def handle_starttag(self, tag, attrs):
        if tag == 'a':
            href = dict(attrs).get('href', '')
            if href.startswith('/') and not href.startswith('//'):
                self.hrefs.append(href)


def _insert(db, table, **values):
    columns = ','.join('"' + name + '"' for name in values)
    return db.execute(f'INSERT INTO "{table}" ({columns}) VALUES ({",".join("?" for _ in values)})', tuple(values.values())).lastrowid


def _seed(dbpath, company, branch, store_branch, other_company, other_branch):
    now = datetime.now(timezone.utc)
    ticks = 621355968000000000 + int(now.timestamp() * 10_000_000)
    identifiers = {}
    with sqlite3.connect(dbpath) as db:
        db.execute('PRAGMA foreign_keys=ON')
        db.execute('BEGIN IMMEDIATE')
        actor = db.execute('SELECT Id FROM AspNetUsers WHERE Email=?', ('farmdemo1@example.invalid',)).fetchone()[0]

        def voucher(c, b, reference, entries):
            assert sum(debit for _, debit, _ in entries) == sum(credit for _, _, credit in entries) > 0
            key = _insert(db, 'Vouchers', CompanyId=c, BranchId=b, Reference=reference, Date=ticks, Posted=1)
            for account, debit, credit in entries:
                _insert(db, 'VoucherLines', VoucherId=key, AccountCode=account, Debit=str(debit), Credit=str(credit))
            return key

        for c, b, count in ((company, branch, 201), (company, store_branch, 1), (other_company, other_branch, 1)):
            local = c == company and b == branch
            prefix = 'PAGING' if local else f'PAGING-PRIVATE-{b}'
            supplier = _insert(db, 'TradingParties', CompanyId=c, BranchId=b, Kind='Supplier', Code='PG-SUP', Name=prefix + ' supplier', Email='', Phone='', Address='', Active=1)
            customer = _insert(db, 'TradingParties', CompanyId=c, BranchId=b, Kind='Customer', Code='PG-CUS', Name=prefix + ' customer', Email='', Phone='', Address='', Active=1)
            item = _insert(db, 'StockItems', CompanyId=c, BranchId=b, SKU='PAGING-ITEM', Name=prefix + ' item', QuantityOnHand=str(count), ReservedQuantity='0.0', UnitCost='1', UnitOfMeasure='KG', RowVersion=b'paging-row-version')
            # Microsoft.Data.Sqlite binds integral decimal concurrency values as
            # "201.0", not "201". Match that TEXT representation for the Value token.
            _insert(db, 'InventoryCostBalances', CompanyId=c, BranchId=b, StockItemId=item, Value=f'{count}.0')
            receipts = []
            for i in range(count):
                purchase_ref = f'{prefix}-PUR-{i:03d}'
                purchase_voucher = voucher(c, b, 'PUR-' + purchase_ref, [('1300', 2, 0), ('2100', 0, 2)])
                receipt = _insert(db, 'PurchaseReceipts', CompanyId=c, BranchId=b, StockItemId=item, Reference=purchase_ref, Quantity='2', UnitPrice='1', VoucherId=purchase_voucher, CreatedUtc=ticks)
                receipts.append(receipt)
                purchase_doc = _insert(db, 'FinanceDocuments', CompanyId=c, BranchId=b, Kind='Purchase', PartyId=supplier, SourceId=receipt, Reference='PUR-' + purchase_ref, Amount='2', CostAmount='2', VoucherId=purchase_voucher, CreatedUtc=ticks)
                _insert(db, 'StockMovements', CompanyId=c, BranchId=b, StockItemId=item, QuantityDelta='2', Reference='PUR-' + purchase_ref, CreatedUtc=ticks)
                sale_ref = f'{prefix}-SAL-{i:03d}'
                sale_voucher = voucher(c, b, 'SAL-' + sale_ref, [('1100', 1, 0), ('4100', 0, 1), ('5100', 1, 0), ('1300', 0, 1)])
                sale = _insert(db, 'ProductSales', CompanyId=c, BranchId=b, StockItemId=item, Reference=sale_ref, Quantity='1', UnitPrice='1', UnitCost='1', VoucherId=sale_voucher, CreatedUtc=ticks)
                sale_doc = _insert(db, 'FinanceDocuments', CompanyId=c, BranchId=b, Kind='Sale', PartyId=customer, SourceId=sale, Reference='SAL-' + sale_ref, Amount='1', CostAmount='1', VoucherId=sale_voucher, CreatedUtc=ticks)
                _insert(db, 'StockMovements', CompanyId=c, BranchId=b, StockItemId=item, QuantityDelta='-1', Reference='SAL-' + sale_ref, CreatedUtc=ticks)
                if local and i == 0:
                    identifiers.update(purchase=purchase_doc, sale=sale_doc, item=item)
            statuses = ['Draft', 'Approved', 'PartiallyReceived'] + ['Cancelled'] * 200 if local else ['Draft']
            for i, status in enumerate(statuses):
                reference = f'{prefix}-ORDER-{i:03d}'
                partial = status == 'PartiallyReceived'
                order = _insert(db, 'PurchaseOrders', CompanyId=c, BranchId=b, Reference=reference, SupplierId=supplier, StockItemId=item,
                                Quantity='4' if partial else '2', UnitPrice='1', Amount='4' if partial else '2', ReceivedQuantity='2' if partial else '0', ReceivedAmount='2' if partial else '0',
                                Status=status, CreatedBy=actor, ApprovedBy=actor if status in ('Approved', 'PartiallyReceived') else None,
                                CreatedUtc=ticks, ApprovedUtc=ticks if status in ('Approved', 'PartiallyReceived') else None, RowVersion=b'paging-order-version')
                if partial:
                    _insert(db, 'PurchaseOrderReceipts', CompanyId=c, BranchId=b, PurchaseOrderId=order, PurchaseReceiptId=receipts[-1], Quantity='2', Amount='2')
                if local and i < 3:
                    identifiers[status] = order
            for i in range(201 if local else 1):
                cash = voucher(c, b, f'{prefix}-CASH-{i:03d}', [('1000', 1, 0), ('3000', 0, 1)])
                if local and i == 0:
                    identifiers['cash'] = cash
            for i in range(101 if local else 1):
                bank = _insert(db, 'BankStatementLines', CompanyId=c, BranchId=b, ExternalReference=f'{prefix}-BANK-{i:03d}', Amount='1', ValueDate=now.date().isoformat(), MatchedVoucherId=None)
                if local and i == 0:
                    identifiers['bank'] = bank
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
    return identifiers


def run_paging_checks(dbpath, admin, Forms, check, page, *, company, branch, store_branch, other_company, other_branch):
    dbpath = Path(dbpath).resolve()
    if not dbpath.is_file() or dbpath.name != 'farm-demo.db' or not dbpath.parent.name.startswith('farm-http-demo-'):
        raise ValueError('Paging acceptance is restricted to the main harness disposable fixture')
    base = urllib.parse.urlsplit(admin.base)
    if base.scheme != 'http' or base.hostname not in ('127.0.0.1', 'localhost', '::1'):
        raise ValueError('Paging acceptance requires the main harness localhost HTTP session')
    ids = _seed(dbpath, company, branch, store_branch, other_company, other_branch)

    def scalar(sql, values=()):
        with sqlite3.connect(dbpath) as db:
            return db.execute(sql, values).fetchone()[0]

    def option_ids(html, action, field):
        return {int(o['value']) for o in Forms(html).get(action)['options'][field] if o['value']}

    def post_form(html, action, field=None, value=None):
        forms = [f for f in Forms(html).forms if f['method'] == 'post' and f['action'] == action and (field is None or f['fields'].get(field) == str(value))]
        if not forms:
            raise AssertionError(f'Expected old-record action form: {action}')
        return forms[0]

    def get_form(html, anchor, changes):
        form = next(f for f in Forms(html).forms if f['method'] == 'get' and urllib.parse.urlsplit(f['action']).fragment == anchor)
        values = dict(form['fields']); values.update(changes)
        path = urllib.parse.urlsplit(form['action']).path + '?' + urllib.parse.urlencode(values)
        return page(admin, path, 'Rendered search form preserves independent paging state')

    accounts = page(admin, '/Accounts', 'Large fixture accounts default page')
    check('Default reconciliation is bounded and excludes older bank line and voucher',
          len(option_ids(accounts.text, '/Accounts/reconcile', 'statementLineId')) <= 100 and
          len(option_ids(accounts.text, '/Accounts/reconcile', 'voucherId')) <= 200 and
          ids['bank'] not in option_ids(accounts.text, '/Accounts/reconcile', 'statementLineId') and
          ids['cash'] not in option_ids(accounts.text, '/Accounts/reconcile', 'voucherId'))
    bank_next = next(href for href in Links(accounts.text).hrefs if urllib.parse.parse_qs(urllib.parse.urlsplit(href).query).get('BankPage') == ['2'])
    bank_page = page(admin, bank_next, 'Actual bank next-page link loads older row')
    check('Older statement is present in rendered reconciliation selector', ids['bank'] in option_ids(bank_page.text, '/Accounts/reconcile', 'statementLineId'))
    voucher_next = next(href for href in Links(bank_page.text).hrefs if urllib.parse.parse_qs(urllib.parse.urlsplit(href).query).get('VoucherPage') == ['2'])
    older = page(admin, voucher_next, 'Actual voucher pager retains older bank page')
    check('Both older reconciliation records are reachable in one rendered form', ids['bank'] in option_ids(older.text, '/Accounts/reconcile', 'statementLineId') and ids['cash'] in option_ids(older.text, '/Accounts/reconcile', 'voucherId'))
    searched = get_form(older.text, 'bank-browse', {'BankSearch': 'PAGING-BANK-000', 'BankUnmatchedOnly': 'true'})
    check('Bank search narrows selector without discarding older voucher page', option_ids(searched.text, '/Accounts/reconcile', 'statementLineId') == {ids['bank']} and ids['cash'] in option_ids(searched.text, '/Accounts/reconcile', 'voucherId'))
    searched = get_form(searched.text, 'voucher-browse', {'VoucherSearch': 'PAGING-CASH-000'})
    check('Independent searches retain both exact old selectable IDs', option_ids(searched.text, '/Accounts/reconcile', 'statementLineId') == {ids['bank']} and option_ids(searched.text, '/Accounts/reconcile', 'voucherId') == {ids['cash']})
    response = admin.submit(Forms(searched.text).get('/Accounts/reconcile'), {'statementLineId': ids['bank'], 'voucherId': ids['cash']})
    check('Oldest displayed bank and voucher reconcile through actual MVC form', response.status == 302 and scalar('SELECT MatchedVoucherId FROM BankStatementLines WHERE Id=?', (ids['bank'],)) == ids['cash'])
    matched = page(admin, '/Accounts?BankSearch=PAGING-BANK-000&BankUnmatchedOnly=true&VoucherSearch=PAGING-CASH-000', 'Completed reconciliation disappears from unmatched selectors')
    check('Matched old bank line and voucher are no longer offered', not option_ids(matched.text, '/Accounts/reconcile', 'statementLineId') and not option_ids(matched.text, '/Accounts/reconcile', 'voucherId'))
    bounds = page(admin, '/Accounts?BankPage=2147483647&VoucherPage=-2147483648', 'Extreme account pages clamp safely')
    check('Clamped account pages remain bounded', len(option_ids(bounds.text, '/Accounts/reconcile', 'statementLineId')) <= 100 and len(option_ids(bounds.text, '/Accounts/reconcile', 'voucherId')) <= 200)
    private = page(admin, f'/Accounts?BankSearch=PAGING-PRIVATE&VoucherSearch=PAGING-PRIVATE&companyId={other_company}&branchId={other_branch}', 'Untrusted query cannot expand reconciliation tenant scope')
    check('Other-company and other-branch matches never enter bank selectors', not option_ids(private.text, '/Accounts/reconcile', 'statementLineId') and not option_ids(private.text, '/Accounts/reconcile', 'voucherId'))

    purchase = page(admin, '/Purchase', 'Large fixture purchase default page')
    check('Default order and return lists do not load unbounded history', ids['purchase'] not in option_ids(purchase.text, '/Purchase/return', 'documentId') and len(option_ids(purchase.text, '/Purchase/return', 'documentId')) <= 200 and
          not any(f['fields'].get('orderId') == str(ids['Draft']) for f in Forms(purchase.text).forms))
    orders_next = next(href for href in Links(purchase.text).hrefs if urllib.parse.parse_qs(urllib.parse.urlsplit(href).query).get('OrderPage') == ['2'])
    orders = page(admin, orders_next, 'Actual order pager exposes older commitment controls')
    check('Older draft, approved and partial orders have real forms', bool(post_form(orders.text, '/Purchase/order/approve', 'orderId', ids['Draft'])) and
          bool(post_form(orders.text, '/Purchase/order/receive', 'orderId', ids['Approved'])) and bool(post_form(orders.text, '/Purchase/order/cancel', 'orderId', ids['PartiallyReceived'])))
    orders = get_form(orders.text, 'order-browse', {'OrderSearch': 'PAGING-ORDER-', 'OrdersOpenOnly': 'true'})
    check('Open-order filter excludes newer cancelled history while retaining old actions', 'PAGING-ORDER-003' not in orders.text and all(f'PAGING-ORDER-{i:03d}' in orders.text for i in range(3)))
    response = admin.submit(post_form(orders.text, '/Purchase/order/approve', 'orderId', ids['Draft']))
    check('Old draft approves through rendered MVC form', response.status == 302 and scalar('SELECT Status FROM PurchaseOrders WHERE Id=?', (ids['Draft'],)) == 'Approved')
    response = admin.submit(post_form(orders.text, '/Purchase/order/receive', 'orderId', ids['Approved']), {'quantity': '1', 'reference': 'PAGING-OLD-ORDER-RECEIPT'})
    order_state = scalar('SELECT Status FROM PurchaseOrders WHERE Id=?', (ids['Approved'],))
    receipt_count = scalar('SELECT COUNT(*) FROM PurchaseOrderReceipts WHERE PurchaseOrderId=?', (ids['Approved'],))
    receipt_ok = response.status == 302 and response.location == '/Purchase' and order_state == 'PartiallyReceived' and receipt_count == 1
    detail = f'HTTP {response.status}; redirect={response.location}; status={order_state}; receipt links={receipt_count}'
    if not receipt_ok:
        feedback = admin.request('/Purchase') if response.status == 302 else response
        alerts = re.findall(r'<div[^>]*role="alert"[^>]*>(.*?)</div>', feedback.text, re.DOTALL)
        detail += '; validation=' + ' | '.join(unescape(re.sub('<[^>]+>', '', text)).strip() for text in alerts)
    check('Old approved commitment receives and posts through MVC', receipt_ok, detail)
    response = admin.submit(post_form(orders.text, '/Purchase/order/cancel', 'orderId', ids['PartiallyReceived']))
    check('Old partial commitment cancels only its remainder through MVC', response.status == 302 and scalar('SELECT Status FROM PurchaseOrders WHERE Id=?', (ids['PartiallyReceived'],)) == 'Cancelled' and Decimal(str(scalar('SELECT ReceivedQuantity FROM PurchaseOrders WHERE Id=?', (ids['PartiallyReceived'],)))) == 2)
    private_orders = page(admin, '/Purchase?OrderSearch=PAGING-PRIVATE', 'Purchase-order search is company and branch scoped')
    check('Private order action forms remain absent', not any(f['action'].startswith('/Purchase/order/') for f in Forms(private_orders.text).forms))

    for route, kind, document, reference in (('/Purchase', 'Purchase', ids['purchase'], 'PUR-PAGING-PUR-000'), ('/Sales', 'Sale', ids['sale'], 'SAL-PAGING-SAL-000')):
        initial = page(admin, route, f'{kind} document selector remains bounded')
        check(f'{kind} oldest return document starts beyond default limit', document not in option_ids(initial.text, route + '/return', 'documentId') and len(option_ids(initial.text, route + '/return', 'documentId')) <= 200)
        next_link = next(href for href in Links(initial.text).hrefs if urllib.parse.parse_qs(urllib.parse.urlsplit(href).query).get('DocumentPage') == ['2'])
        old = page(admin, next_link, f'Actual {kind} document pager exposes older return')
        check(f'{kind} older ID is present in rendered return selector', document in option_ids(old.text, route + '/return', 'documentId'))
        old = get_form(old.text, 'document-browse', {'DocumentSearch': reference})
        check(f'{kind} reference search selects only the older document', option_ids(old.text, route + '/return', 'documentId') == {document})
        response = admin.submit(Forms(old.text).get(route + '/return'), {'documentId': document, 'quantity': '1', 'reference': f'PAGING-OLD-{kind.upper()}-RETURN'})
        check(f'{kind} older return posts through real MVC form', response.status == 302 and scalar('SELECT COUNT(*) FROM StockDocumentReturns WHERE FinanceDocumentId=?', (document,)) == 1)
        isolated = page(admin, route + '?DocumentSearch=PAGING-PRIVATE', f'{kind} document search remains tenant scoped')
        check(f'{kind} private return document is never selectable', not option_ids(isolated.text, route + '/return', 'documentId'))

    with sqlite3.connect(dbpath) as db:
        check('Paging fixture and actual actions preserve relational integrity', not db.execute('PRAGMA foreign_key_check').fetchall())
        stock = Decimal(str(db.execute('SELECT QuantityOnHand FROM StockItems WHERE Id=?', (ids['item'],)).fetchone()[0]))
        movements = sum(Decimal(str(row[0])) for row in db.execute('SELECT QuantityDelta FROM StockMovements WHERE StockItemId=?', (ids['item'],)))
        carrying = Decimal(str(db.execute('SELECT Value FROM InventoryCostBalances WHERE StockItemId=?', (ids['item'],)).fetchone()[0]))
        check('Paging fixtures and oldest actions retain exact stock and carrying values', stock == movements == carrying)
    admin_helper = runpy.run_path(str(Path(__file__).with_name('http-admin-paging-smoke.py')))
    admin_helper['run_admin_paging_checks'](dbpath, admin, Forms, check, page, company=company, branch=branch, other_company=other_company)
