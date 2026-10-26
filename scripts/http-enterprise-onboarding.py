#!/usr/bin/env python3
"""Fresh account → explicit offline ownership review → own HR/customer access.
Only synthetic, disposable localhost SQLite; never sends email or verifies a real mailbox.
"""
from enterprise_http_fixture import Fixture, Forms, Session
import json, re
from datetime import datetime
from zoneinfo import ZoneInfo

# Check the documented Windows dependency before creating the disposable host.
BUSINESS_TIME_ZONE = ZoneInfo('Asia/Dhaka')

with Fixture('onboarding') as f:
    admin=f.login(1); outsider=f.login(2)
    company=f.scalar("SELECT Id FROM Companies WHERE Slug='demo-farm-1'")
    branch=f.scalar('SELECT Id FROM Branches WHERE CompanyId=? AND Name=?',(company,'Demo Branch 1'))
    email='http-onboarded@example.invalid'
    create=f.form(admin,'/Administration','/Administration/staff/create')
    create['fields'].pop('CreateStaff.Permissions',None)
    created=admin.submit(create,{'CreateStaff.Email':email,'CreateStaff.Password':f.password,'CreateStaff.BranchId':branch,'CreateStaff.Permissions[0]':'Employee.Self','CreateStaff.Permissions[1]':'Ecommerce.View'})
    f.check('Create fresh account using actual administration form',created.status==302)
    uid=f.scalar('SELECT Id FROM AspNetUsers WHERE Email=?',(email,))
    f.check('Actual account form grants exactly the two selected operational permissions',set(r[0] for r in f.query('SELECT Permission FROM UserPermissions WHERE UserId=?',(uid,)))=={'Employee.Self','Ecommerce.View'})
    f.check('New account is unconfirmed until explicit ownership review',f.scalar('SELECT EmailConfirmed FROM AspNetUsers WHERE Id=?',(uid,))==0)
    verification=f.form(admin,'/Administration','/Administration/staff/verify-email-ownership')
    f.check('Review names exact account and does not pretend to send SMTP',verification['fields']['VerifyEmail.UserId']==uid and verification['fields']['VerifyEmail.Email']==email and 'ConcurrencyStamp' in ''.join(verification['fields']))
    values={'VerifyEmail.Method':'ObservedMailboxControl','VerifyEmail.EvidenceReference':'HTTP-FICTIONAL-OWNERSHIP-001','VerifyEmail.CurrentPassword':f.password,'VerifyEmail.Confirm':'true'}
    no_csrf=dict(verification['fields']);no_csrf.update(values);no_csrf.pop('__RequestVerificationToken',None)
    f.check('Ownership mutation requires CSRF',admin.request(verification['action'],fields=no_csrf).status==400)
    missing=admin.submit(verification,{**values,'VerifyEmail.Confirm':'false'})
    f.check('Ownership review requires explicit attestation',missing.status==200 and f.scalar('SELECT EmailConfirmed FROM AspNetUsers WHERE Id=?',(uid,))==0)
    wrong=admin.submit(verification,{**values,'VerifyEmail.CurrentPassword':'Incorrect-fixture-only-password'})
    f.check('Wrong administrator password cannot verify target or leak attempted password',wrong.status==200 and 'Incorrect-fixture-only-password' not in wrong.text and f.password not in wrong.text and f.scalar('SELECT EmailConfirmed FROM AspNetUsers WHERE Id=?',(uid,))==0)
    fresh=f.form(admin,'/Administration','/Administration/staff/verify-email-ownership')
    foreign=dict(fresh['fields']);foreign.update(values)
    foreign['__RequestVerificationToken']=f.form(outsider,'/Context','/Context/Select')['fields']['__RequestVerificationToken']
    f.check('Another company cannot attest guessed target identity',outsider.request(fresh['action'],fields=foreign).status==403 and f.scalar('SELECT EmailConfirmed FROM AspNetUsers WHERE Id=?',(uid,))==0)
    confirmed=admin.submit(fresh,values)
    f.check('Scoped password-confirmed offline review verifies new account',confirmed.status==302 and f.scalar('SELECT EmailConfirmed FROM AspNetUsers WHERE Id=?',(uid,))==1)
    rows=f.query("SELECT ActorId,Reference FROM Audits WHERE CompanyId=? AND Action='StaffEmailOwnershipVerifiedOffline'",(company,))
    f.check('Complete compact offline audit fits SQL Server column and names method/reference',len(rows)==1 and len(rows[0][1])<=200 and json.loads(rows[0][1])['u']==uid and json.loads(rows[0][1])['m']=='M' and json.loads(rows[0][1])['r']=='HTTP-FICTIONAL-OWNERSHIP-001' and len(json.loads(rows[0][1])['e'])==64)
    replay=admin.submit(fresh,values)
    f.check('Replayed verification does not add review evidence',replay.status==200 and f.scalar("SELECT COUNT(*) FROM Audits WHERE Action='StaffEmailOwnershipVerifiedOffline'")==1)
    staff_form=f.form(admin,'/HRPayroll/staff','/HRPayroll/staff')
    response=admin.submit(staff_form,{'Code':'HTTP-OWNED-STAFF','Name':'HTTP fictional account owner','JobTitle':'Synthetic onboarding review','StartDate':datetime.now(BUSINESS_TIME_ZONE).date().isoformat(),'EndDate':'','Active':'true'})
    f.check('Create staff record without fixture-level identity mutation',response.status==302,'HTTP '+str(response.status)+'; '+str(re.findall(r'<li>(.*?)</li>',response.text,re.S)))
    sid=f.scalar("SELECT Id FROM HRPayrollStaff WHERE Code='HTTP-OWNED-STAFF'")
    link=f.form(admin,f'/HRPayroll/identity/{sid}',f'/HRPayroll/identity/{sid}')
    response=admin.submit(link,{'UserId':uid,'Confirm':'true'})
    f.check('Newly reviewed account can be explicitly linked to own employee record',response.status==302 and f.scalar('SELECT IdentityUserId FROM HRPayrollStaff WHERE Id=?',(sid,))==uid)
    party_form=f.form(admin,'/Parties','/Parties/create')
    response=admin.submit(party_form,{'kind':'Customer','code':'HTTP-OWNED-CUSTOMER','name':'HTTP fictional account customer','email':'','phone':'','address':''})
    f.check('Create customer party through real form',response.status==302)
    party=f.scalar("SELECT Id FROM TradingParties WHERE Code='HTTP-OWNED-CUSTOMER'")
    link=f.form(admin,'/Ecommerce/manage','/Ecommerce/customer-link')
    response=admin.submit(link,{'existingUserId':uid,'partyId':party,'confirmIdentityVerified':'true'})
    f.check('Newly reviewed account can be explicitly linked to own customer record',response.status==302 and f.scalar('SELECT COUNT(*) FROM EcommerceCustomers WHERE UserId=?',(uid,))==1)
    owner=Session(f.base)
    login=Forms(f.page(owner,'/Account/Login').text).get('/Account/Login')
    response=owner.submit(login,{'email':email,'password':f.password})
    f.check('New owner logs in with original unchanged password',response.status==302 and response.location=='/Context')
    context=Forms(f.page(owner,'/Context').text).get('/Context/Select')
    response=owner.submit(context,{'companyId':company,'branchId':branch})
    f.check('New owner selects authorized company and branch',response.status==302)
    ownhr=f.page(owner,'/HRPayroll/me')
    f.check('Employee self-service is reachable after genuine onboarding flow','HTTP fictional account owner' in ownhr.text)
    owncart=f.page(owner,'/Ecommerce/cart')
    f.check('Customer cart is reachable after genuine onboarding flow','HTTP fictional account customer' in owncart.text)
    f.check('Onboarding granted no posting or administration permission',f.scalar("SELECT COUNT(*) FROM UserPermissions WHERE UserId=? AND Permission NOT IN ('Employee.Self','Ecommerce.View')",(uid,))==0)
