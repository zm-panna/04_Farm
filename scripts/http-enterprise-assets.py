#!/usr/bin/env python3
"""Real MVC/Identity/CSRF fixed-asset journey in a disposable synthetic localhost database.
This is HTTP acceptance, not browser/JavaScript, production, statutory or SQL Server approval.
"""
from datetime import date, timedelta
from decimal import Decimal
from enterprise_http_fixture import Fixture, Forms


def run():
    with Fixture('assets') as f:
        maker, reviewer, outsider = f.login(1), f.login(1, reviewer=True), f.login(2)
        for table in ('FixedAssets', 'FixedAssetCustodies', 'FixedAssetDepreciationRuns', 'FixedAssetDepreciationLines', 'FixedAssetDisposals'):
            f.check('Fictional minimum rows: '+table, f.scalar('SELECT COUNT(*) FROM '+table) >= 5)
        first = date.today().replace(day=1)
        last_period = first-timedelta(days=1)
        acquired = last_period.replace(day=1)
        before = f.scalar('SELECT COUNT(*) FROM FixedAssets')
        form = f.form(maker, '/fixed-assets/new', '/fixed-assets/new')
        f.page(maker, '/fixed-assets')
        f.check('Cancel navigation from acquisition form does not write', f.scalar('SELECT COUNT(*) FROM FixedAssets') == before)
        fields = {'AssetCode':'HTTP-ASSET-001','Name':'HTTP irrigation pump <script>invalid()</script>', 'Category':'HTTP equipment', 'SerialNumber':'HTTP-SERIAL-1',
            'Notes':'Fictional local HTTP verification only','SourceReference':'HTTP-ASSET-INVOICE-001','AcquisitionDate':str(acquired), 'Cost':'1000.00',
            'AcquisitionSettlement':'Cash','DepreciationMethod':'StraightLineMonthly','UsefulLifeMonths':'12','ResidualValue':'100.00','DepreciationStart':str(acquired),
            'PolicyReason':'Synthetic explicitly selected book estimate; no statutory rate','Custodian':'HTTP maker','Location':'HTTP pump house'}
        response = maker.request('/fixed-assets/new', fields)
        f.check('Missing CSRF token blocks acquisition', response.status == 400 and f.scalar('SELECT COUNT(*) FROM FixedAssets') == before)
        response = maker.submit(form, fields)
        f.check('Real model binding creates acquisition draft', response.status == 302 and response.location.startswith('/fixed-assets/'), 'HTTP '+str(response.status))
        path = response.location; asset = int(path.rsplit('/',1)[-1])
        f.check('Draft holds reviewed policy snapshot but no journal', f.query('SELECT Status,CurrencyCode,AcquisitionVoucherId FROM FixedAssets WHERE Id=?',(asset,))[0] == ('Draft','BDT',None))
        page = f.page(maker,path)
        f.check('User-entered asset description is HTML encoded', '<script>invalid()</script>' not in page.text and '&lt;script&gt;' in page.text)
        original = Forms(page.text).get(path+'/approve'); old_token=original['fields']['RowVersion']
        response = maker.submit(original, {'ConfirmReviewed':'true','ReviewNote':'Synthetic self review attempt'})
        f.check('Acquisition maker cannot approve own policy', response.status == 400 and f.scalar('SELECT Status FROM FixedAssets WHERE Id=?',(asset,)) == 'Draft')
        approved = reviewer.submit(f.form(reviewer,path,path+'/approve'),{'ConfirmReviewed':'true','ReviewNote':'Independent synthetic acquisition review'})
        f.check('Different identity approves acquisition', approved.status == 302)
        post = f.form(maker,path,path+'/post')
        stale = maker.submit(post,{'RowVersion':old_token})
        f.check('Stale original acquisition token conflicts', stale.status == 409 and f.scalar('SELECT Status FROM FixedAssets WHERE Id=?',(asset,)) == 'Approved')
        response=maker.submit(post)
        f.check('Approved acquisition posts once', response.status == 302 and f.scalar('SELECT Status FROM FixedAssets WHERE Id=?',(asset,)) == 'Active')
        f.check('Acquisition journal has equal debit and credit', f.scalar('SELECT ROUND(SUM(Debit)-SUM(Credit),3) FROM VoucherLines WHERE VoucherId=(SELECT AcquisitionVoucherId FROM FixedAssets WHERE Id=?)',(asset,)) == 0)
        replay=maker.submit(post)
        f.check('Repeated acquisition submit cannot duplicate journal', replay.status == 409 and f.scalar('SELECT COUNT(*) FROM Vouchers WHERE Reference=?',(f'FA-ACQ-{f.scalar("SELECT BranchId FROM FixedAssets WHERE Id=?",(asset,))}-{asset}',)) == 1)
        edit1=f.form(maker,path+'/edit',path+'/edit');edit2=f.form(maker,path+'/edit',path+'/edit')
        response=maker.submit(edit1,{'Name':'HTTP reviewed irrigation pump'})
        f.check('Nonfinancial edit posts successfully', response.status == 302)
        response=maker.submit(edit2,{'Name':'STALE HTTP overwrite'})
        f.check('Stale metadata form preserves original token and rejects overwrite', response.status == 409 and Forms(response.text).get(path+'/edit')['fields']['RowVersion'] == edit2['fields']['RowVersion'] and f.scalar('SELECT Name FROM FixedAssets WHERE Id=?',(asset,)) == 'HTTP reviewed irrigation pump')
        custody=f.form(maker,path+'/custody',path+'/custody')
        response=maker.submit(custody,{'Input.EffectiveDate':str(acquired+timedelta(days=1)),'Input.Custodian':'HTTP new custodian','Input.Location':'HTTP north shed','Input.Reason':'Synthetic handover evidence'})
        f.check('Custody model binds and appends history', response.status == 302 and f.scalar('SELECT COUNT(*) FROM FixedAssetCustodies WHERE FixedAssetId=?',(asset,)) == 2)
        runform=f.form(maker,'/fixed-assets/depreciation/new','/fixed-assets/depreciation/new')
        response=maker.submit(runform,{'Reference':'HTTP-ASSET-DEP-001','Period':str(last_period)})
        f.check('Monthly preview saves through real form', response.status == 302 and response.location.startswith('/fixed-assets/depreciation/'))
        runpath=response.location;runid=int(runpath.rsplit('/',1)[-1]);f.check('Monthly charge matches explicit policy', Decimal(str(f.scalar('SELECT Total FROM FixedAssetDepreciationRuns WHERE Id=?',(runid,)))) == Decimal('75'))
        response=reviewer.submit(f.form(reviewer,runpath,runpath+'/approve'),{'ConfirmReviewed':'true','ReviewNote':'Independent synthetic monthly review'})
        f.check('Monthly preview is independently approved',response.status==302)
        runpost=f.form(maker,runpath,runpath+'/post');response=maker.submit(runpost)
        f.check('Monthly posting updates exact asset balance',response.status==302 and Decimal(str(f.scalar('SELECT AccumulatedDepreciation FROM FixedAssets WHERE Id=?',(asset,))))==Decimal('75'))
        response=maker.submit(runpost);f.check('Repeated monthly submit cannot duplicate asset period',response.status==409 and f.scalar('SELECT COUNT(*) FROM FixedAssetDepreciationLines WHERE FixedAssetId=? AND Posted=1',(asset,))==1)
        response=maker.submit(f.form(maker,path+'/dispose',path+'/dispose'),{'Input.Reference':'HTTP-ASSET-DSP-001','Input.DisposalDate':str(first),'Input.Counterparty':'HTTP fictional buyer','Input.Reason':'Synthetic equipment replacement','Input.Proceeds':'950.00','Input.Settlement':'Receivable'})
        f.check('Disposal preview binds current asset token',response.status==302 and response.location.startswith('/fixed-assets/disposals/'))
        disposalpath=response.location;disposalid=int(disposalpath.rsplit('/',1)[-1])
        f.check('Disposal preview has exact book value and gain',tuple(Decimal(str(x)) for x in f.query('SELECT CarryingValue,GainLoss FROM FixedAssetDisposals WHERE Id=?',(disposalid,))[0])==(Decimal('925'),Decimal('25')))
        for target in (path,runpath,disposalpath):f.check('Other company cannot read '+target,outsider.request(target).status==404)
        outsiderform=f.form(outsider,'/fixed-assets/new','/fixed-assets/new')
        response=outsider.request(disposalpath+'/post',{'__RequestVerificationToken':outsiderform['fields']['__RequestVerificationToken'],'RowVersion':'AQID'})
        f.check('Other company cannot mutate disposal even with own CSRF',response.status==404)
        response=reviewer.submit(f.form(reviewer,disposalpath,disposalpath+'/approve'),{'ConfirmReviewed':'true','ReviewNote':'Independent synthetic disposal review'})
        f.check('Different user approves disposal',response.status==302)
        disposalpost=f.form(maker,disposalpath,disposalpath+'/post');response=maker.submit(disposalpost)
        f.check('Disposal posts and derecognizes once',response.status==302 and f.scalar('SELECT Status FROM FixedAssets WHERE Id=?',(asset,))=='Disposed')
        f.check('Disposal balanced receivable and gain journal',f.scalar('SELECT ROUND(SUM(Debit)-SUM(Credit),3) FROM VoucherLines WHERE VoucherId=(SELECT VoucherId FROM FixedAssetDisposals WHERE Id=?)',(disposalid,))==0
            and Decimal(str(f.scalar("SELECT Credit FROM VoucherLines WHERE AccountCode='4800' AND VoucherId=(SELECT VoucherId FROM FixedAssetDisposals WHERE Id=?)",(disposalid,))))==Decimal('25'))
        response=maker.submit(disposalpost);f.check('Repeated disposal cannot post twice',response.status==409 and f.scalar("SELECT COUNT(*) FROM FixedAssetDisposals WHERE FixedAssetId=? AND Status='Posted'",(asset,))==1)
        f.page(maker,'/fixed-assets?q=HTTP-ASSET&status=Disposed&page=1')
        cancel_fields=dict(fields,AssetCode='HTTP-ASSET-CANCEL',SourceReference='HTTP-ASSET-CANCEL-INVOICE')
        response=maker.submit(f.form(maker,'/fixed-assets/new','/fixed-assets/new'),cancel_fields)
        f.check('Second acquisition remains cancellable draft',response.status==302)
        cancelpath=response.location;cancelid=int(cancelpath.rsplit('/',1)[-1]);cancelform=f.form(maker,cancelpath,cancelpath+'/cancel')
        response=maker.submit(cancelform,{'ReviewNote':'Synthetic cancellation with no accounting effects'})
        f.check('Explicit cancellation retains history with no journal',response.status==302 and f.query('SELECT Status,AcquisitionVoucherId FROM FixedAssets WHERE Id=?',(cancelid,))[0]==('Cancelled',None))
        response=maker.submit(cancelform,{'ReviewNote':'Replay'});f.check('Cancelled form replay conflicts',response.status==409)
        f.check('HTTP journey audit retained',f.scalar("SELECT COUNT(*) FROM Audits WHERE Action LIKE 'FixedAssets.%' AND Reference LIKE ?",(f'%Asset {asset}%',))>=5)

if __name__=='__main__':run()
