#!/usr/bin/env python3
"""Real localhost MVC policy/tax/AI acceptance in an ephemeral fictional enterprise fixture.
No remote endpoint or existing database is accepted; no statutory rate is asserted.
"""
import argparse
from datetime import date, timedelta
from decimal import Decimal
from enterprise_http_fixture import Fixture, Forms


def selected_form(f, session, action, row_id):
    forms=Forms(f.page(session,'/business-policy').text).forms
    matches=[x for x in forms if x['action']==action and x['fields'].get('id')==str(row_id)]
    if len(matches)!=1:raise AssertionError('Expected one current review form for '+action+' row '+str(row_id))
    return matches[0]


def snapshot(f):
    # Synthetic business rows stay in memory only, never in evidence files.
    names=[x[0] for x in f.query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'AspNet%' AND name NOT LIKE 'sqlite_%' ORDER BY name")]
    return {name:sorted(f.query('SELECT * FROM "'+name.replace('"','""')+'"'),key=repr) for name in names}


def run(skip_blank_region=False):
    with Fixture('policy') as f:
        if skip_blank_region:f.report['not_verified']=['Blank optional RegionCode binding: known pending source fix; rerun without --skip-blank-region on final binary.']
        maker,reviewer,outsider=f.login(1),f.login(1,reviewer=True),f.login(2)
        company=f.scalar("SELECT Id FROM Companies WHERE Slug='demo-farm-1'");other_company=f.scalar("SELECT Id FROM Companies WHERE Slug='demo-farm-2'")
        branch=f.scalar('SELECT Id FROM Branches WHERE CompanyId=?',(company,));user=f.scalar("SELECT Id FROM AspNetUsers WHERE Email='farmdemo1@example.invalid'")
        future=str(date.today()+timedelta(days=2));region='HTTP-REGION' if skip_blank_region else ''
        financial_before=f.scalar('SELECT COUNT(*) FROM Vouchers')
        company_count=lambda:f.scalar('SELECT COUNT(*) FROM CompanyBusinessPolicies WHERE CompanyId=?',(company,))
        tax_count=lambda:f.scalar('SELECT COUNT(*) FROM TaxPolicies WHERE CompanyId=?',(company,))
        form=f.form(maker,'/business-policy','/business-policy/propose')
        fields={'CountryCode':'BD','RegionCode':region,'CurrencyCode':'BDT','CurrencyMinorUnits':'2','TimeZoneId':'Asia/Dhaka','EffectiveFrom':future,'EffectiveTo':'',
            'ReviewBasis':'HTTP fictional explicit policy A','ExistingLedgerCurrencyConfirmed':'true'}
        before=company_count();response=maker.request('/business-policy/propose',fields)
        f.check('Company policy requires CSRF',response.status==400 and company_count()==before)
        missing=dict(form['fields']);missing.update(fields);missing.pop('CurrencyMinorUnits',None)
        response=maker.request('/business-policy/propose',missing)
        f.check('Omitted currency precision is rejected, never defaulted',response.status==302 and company_count()==before)
        response=maker.submit(form,dict(fields,CurrencyMinorUnits=''))
        f.check('Blank currency precision is rejected',response.status==302 and company_count()==before)
        response=maker.submit(form,dict(fields,CurrencyCode='USD'))
        f.check('Currency change cannot relabel existing books',response.status==302 and company_count()==before and f.scalar('SELECT CurrencyCode FROM CompanyCurrencyLocks WHERE CompanyId=?',(company,))=='BDT')
        response=maker.submit(form,dict(fields,ExistingLedgerCurrencyConfirmed='false'))
        f.check('Existing ledger denomination requires explicit confirmation',response.status==302 and company_count()==before)
        response=maker.submit(form,fields)
        f.check('Same-currency policy proposal binds through real MVC form',response.status==302 and company_count()==before+1)
        a=f.scalar('SELECT Id FROM CompanyBusinessPolicies WHERE CompanyId=? AND ReviewBasis=?',(company,fields['ReviewBasis']))
        row=f.query('SELECT RegionCode,EffectiveTo,Status,CurrencyCode,CurrencyMinorUnits FROM CompanyBusinessPolicies WHERE Id=?',(a,))[0]
        f.check('Optional effective-through binds as null',row[1] is None and row[2:] == ('Draft','BDT',2))
        if not skip_blank_region:f.check('Optional blank non-US region binds and remains blank',row[0]=='')
        before=company_count();response=maker.submit(form,dict(fields,CountryCode='US',RegionCode='',ReviewBasis='HTTP invalid US without state'))
        f.check('US policy requires an explicitly identified state',response.status==302 and company_count()==before)
        response=maker.submit(form,dict(fields,ReviewBasis='HTTP fictional competing policy B'))
        f.check('Competing unreviewed proposal has a separate version',response.status==302 and company_count()==before+1)
        b=f.scalar('SELECT Id FROM CompanyBusinessPolicies WHERE CompanyId=? AND ReviewBasis=?',(company,'HTTP fictional competing policy B'))
        response=maker.submit(selected_form(f,maker,'/business-policy/review',a),{'approve':'true'})
        f.check('Policy proposer cannot approve own proposal',response.status==302 and f.scalar('SELECT Status FROM CompanyBusinessPolicies WHERE Id=?',(a,))=='Draft')
        check_a=selected_form(f,reviewer,'/business-policy/review',a)
        outside_form=f.form(outsider,'/business-policy','/business-policy/propose')
        response=outsider.request('/business-policy/review',{'__RequestVerificationToken':outside_form['fields']['__RequestVerificationToken'],'id':a,'versionToken':check_a['fields']['versionToken'],'approve':'true'})
        f.check('Other company cannot review a foreign company policy',response.status==302 and f.scalar('SELECT Status FROM CompanyBusinessPolicies WHERE Id=?',(a,))=='Draft')
        response=reviewer.submit(check_a,{'approve':'true'})
        f.check('Different identity approves same-currency version',response.status==302 and f.scalar('SELECT Status FROM CompanyBusinessPolicies WHERE Id=?',(a,))=='Approved')
        response=reviewer.submit(check_a,{'approve':'false'})
        f.check('Stale original review cannot reverse approved policy',response.status==302 and f.scalar('SELECT Status FROM CompanyBusinessPolicies WHERE Id=?',(a,))=='Approved')
        check_b=selected_form(f,reviewer,'/business-policy/review',b)
        response=reviewer.submit(check_b,{'approve':'true'})
        f.check('Competing same-effective-date version cannot supersede approved version',response.status==302 and f.scalar('SELECT Status FROM CompanyBusinessPolicies WHERE Id=?',(b,))=='Draft')
        response=reviewer.submit(check_b,{'approve':'false'})
        f.check('Obsolete proposal can be explicitly rejected',response.status==302 and f.scalar('SELECT Status FROM CompanyBusinessPolicies WHERE Id=?',(b,))=='Rejected')
        f.check('Original approved company policy remains immutable history',f.query('SELECT CurrencyCode,Status,EffectiveFrom FROM CompanyBusinessPolicies WHERE CompanyId=? AND Version=1',(company,))[0]==('BDT','Approved','2000-01-01'))
        f.check('Other company listing excludes policy source evidence','HTTP fictional explicit policy A' not in f.page(outsider,'/business-policy').text)
        taxform=f.form(maker,'/business-policy','/business-policy/tax/propose')
        taxfields={'Code':'HTTP-ZERO','Name':'HTTP synthetic zero-tax review','TaxTreatment':'Exempt','RatePercent':'0','TaxLiabilityAccount':'2200','EffectiveFrom':future,'EffectiveTo':'','Reason':'Synthetic explicitly reviewed exemption basis, not a legal claim'}
        before=tax_count();missing=dict(taxform['fields']);missing.update(taxfields);missing.pop('RatePercent',None)
        response=maker.request('/business-policy/tax/propose',missing)
        f.check('Omitted tax percentage is rejected instead of defaulting to zero',response.status==302 and tax_count()==before)
        response=maker.submit(taxform,dict(taxfields,RatePercent=''))
        f.check('Blank tax percentage is rejected',response.status==302 and tax_count()==before)
        response=maker.submit(taxform,dict(taxfields,TaxTreatment='Standard'))
        f.check('Zero rate cannot masquerade as standard positive tax',response.status==302 and tax_count()==before)
        response=maker.submit(taxform,dict(taxfields,Reason=''))
        f.check('Zero exemption needs a specific review basis',response.status==302 and tax_count()==before)
        response=maker.submit(taxform,taxfields)
        f.check('Explicit zero exemption proposal saves',response.status==302 and tax_count()==before+1)
        tax_a=f.scalar("SELECT Id FROM TaxPolicies WHERE CompanyId=? AND Code='HTTP-ZERO' AND Version=1",(company,))
        taxrow=f.query('SELECT RatePercent,TaxTreatment,Reason,EffectiveTo,CompanyBusinessPolicyId,Status FROM TaxPolicies WHERE Id=?',(tax_a,))[0]
        f.check('Tax zero, reason and effective company policy are snapshotted exactly',Decimal(str(taxrow[0]))==0 and taxrow[1]=='Exempt' and taxrow[2]==taxfields['Reason'] and taxrow[3] is None and taxrow[4]==a and taxrow[5]=='Draft')
        response=maker.submit(taxform,dict(taxfields,Reason='HTTP competing zero-tax proposal'))
        f.check('Tax competing proposal retains separate version',response.status==302 and tax_count()==before+2)
        tax_b=f.scalar("SELECT Id FROM TaxPolicies WHERE CompanyId=? AND Code='HTTP-ZERO' AND Version=2",(company,))
        response=maker.submit(selected_form(f,maker,'/business-policy/tax/review',tax_a),{'approve':'true'})
        f.check('Tax proposer cannot approve own exemption',response.status==302 and f.scalar('SELECT Status FROM TaxPolicies WHERE Id=?',(tax_a,))=='Draft')
        approve_tax=selected_form(f,reviewer,'/business-policy/tax/review',tax_a)
        response=outsider.request('/business-policy/tax/review',{'__RequestVerificationToken':outside_form['fields']['__RequestVerificationToken'],'id':tax_a,'versionToken':approve_tax['fields']['versionToken'],'approve':'true'})
        f.check('Tax review cannot cross company scope',response.status==302 and f.scalar('SELECT Status FROM TaxPolicies WHERE Id=?',(tax_a,))=='Draft')
        response=reviewer.submit(approve_tax,{'approve':'true'})
        f.check('Different reviewer approves explicit zero exemption',response.status==302 and f.scalar('SELECT Status FROM TaxPolicies WHERE Id=?',(tax_a,))=='Approved')
        response=reviewer.submit(approve_tax,{'approve':'false'})
        f.check('Stale tax review cannot rewrite approved rate',response.status==302 and f.scalar('SELECT Status FROM TaxPolicies WHERE Id=?',(tax_a,))=='Approved')
        tax_b_form=selected_form(f,reviewer,'/business-policy/tax/review',tax_b)
        response=reviewer.submit(tax_b_form,{'approve':'true'})
        f.check('Competing tax version is blocked after another approval',response.status==302 and f.scalar('SELECT Status FROM TaxPolicies WHERE Id=?',(tax_b,))=='Draft')
        response=reviewer.submit(tax_b_form,{'approve':'false'})
        f.check('Obsolete tax draft may be rejected without changing approved version',response.status==302 and f.scalar('SELECT Status FROM TaxPolicies WHERE Id=?',(tax_b,))=='Rejected')
        f.check('Policy and tax review never create financial journal entries',f.scalar('SELECT COUNT(*) FROM Vouchers')==financial_before)
        f.check('Foreign company tax list does not disclose review basis','HTTP synthetic zero-tax review' not in f.page(outsider,'/business-policy').text)
        manager=f.page(maker,'/AIManager');options=Forms(manager.text).get('/AIManager/draft')['options']['module']
        f.check('AI Manager lists only allowed module identifiers',{x['value'] for x in options}=={'hr','payroll','crm','assets','ecommerce'})
        response=maker.request('/AIManager/facts/assets');data=__import__('json').loads(response.text)
        f.check('AI fact snapshot exposes exact active company and branch',response.status==200 and data['companyId']==company and data['branchId']==branch and data['currencyCode']=='BDT')
        counts={x['label']:x['count'] for x in data['facts']}
        f.check('AI asset counts are permission-scoped live database facts',counts['Disposed assets']==f.scalar("SELECT COUNT(*) FROM FixedAssets WHERE CompanyId=? AND BranchId=? AND Status='Disposed'",(company,branch)))
        response=outsider.request('/AIManager/facts/assets');other=__import__('json').loads(response.text)
        f.check('Other user AI snapshot remains in other company',response.status==200 and other['companyId']==other_company and other['companyId']!=data['companyId'])
        before=snapshot(f);draftform=Forms(manager.text).get('/AIManager/draft');response=maker.submit(draftform,{'module':'assets'})
        f.check('AI Manager renders a truthful local review draft',response.status==200 and 'Deterministic review draft from live permission-scoped facts' in response.text and 'This draft makes no accounting entries.' in response.text)
        f.check('AI review draft leaves every business row unchanged',before==snapshot(f))
        response=maker.request('/AIManager/draft',{'module':'assets'})
        f.check('AI draft POST requires CSRF',response.status==400)
        f.check('Unknown AI module is rejected',maker.request('/AIManager/facts/admin').status==403)
        # Fixture-only grant changes prove current authorization is rechecked on every request.
        f.query("DELETE FROM UserPermissions WHERE CompanyId=? AND UserId=? AND Permission IN ('*','FixedAssets.View')",(company,user))
        f.check('Permission boundary fixture removes demo administrator wildcard',f.scalar("SELECT COUNT(*) FROM UserPermissions WHERE CompanyId=? AND UserId=? AND Permission='*'",(company,user))==0)
        allowed=Forms(f.page(maker,'/AIManager').text).get('/AIManager/draft')
        f.check('Missing asset grant hides the asset choice',not any(x['value']=='assets' for x in allowed['options']['module']))
        f.check('AI.Read alone cannot read asset facts',maker.request('/AIManager/facts/assets').status==403)
        f.check('Forged AI draft module cannot bypass module grant',maker.submit(allowed,{'module':'assets'}).status==403)
        f.check('Other granted AI module stays accessible',maker.request('/AIManager/facts/crm').status==200)
        f.query("INSERT INTO UserPermissions (CompanyId,UserId,Permission) VALUES (?,?,'FixedAssets.View')",(company,user))
        f.query("DELETE FROM UserPermissions WHERE CompanyId=? AND UserId=? AND Permission='AI.Read'",(company,user))
        f.check('Business-module permission alone cannot enter AI Manager',maker.request('/AIManager').status==403)
        f.check('Business-module permission alone cannot read AI facts',maker.request('/AIManager/facts/assets').status==403)
        f.query("INSERT INTO UserPermissions (CompanyId,UserId,Permission) VALUES (?,?,'AI.Read')",(company,user))
        farm=f.scalar('SELECT FarmId FROM Branches WHERE Id=?',(branch,));f.query('INSERT INTO Branches (CompanyId,FarmId,Name) VALUES (?,?,?)',(company,farm,'HTTP isolated empty branch'))
        empty=f.scalar("SELECT Id FROM Branches WHERE CompanyId=? AND Name='HTTP isolated empty branch'",(company,));f.query('INSERT INTO UserBranchAccess (UserId,CompanyId,BranchId) VALUES (?,?,?)',(user,company,empty))
        second=f.login(1,branch=empty);response=second.request('/AIManager/facts/assets');emptyfacts=__import__('json').loads(response.text)
        f.check('AI facts honor selected branch even inside the same company',response.status==200 and emptyfacts['branchId']==empty and all(x['count']==0 for x in emptyfacts['facts']))
        response=maker.request('/AIManager/facts/assets');original=__import__('json').loads(response.text)
        f.check('Another browser session keeps its own original branch',response.status==200 and original['branchId']==branch and any(x['count']>0 for x in original['facts']))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--skip-blank-region',action='store_true',help='Checkpoint only: mark pending optional-region binding test unverified. Final acceptance must omit this flag.')
    run(p.parse_args().skip_blank_region)
