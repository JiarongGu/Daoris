import { useTranslation } from 'react-i18next';
import { Button, Icon, Menu } from '../../design/ui';

/** A row's ⋯ in the Roles mode (COMPANYVIEW1f, the company-view design §4): *On the diagram*, its node chosen there. */
export function RowMore({ name, onDiagram }: { name: string; onDiagram: () => void }) {
  const { t } = useTranslation();
  return (
    <Menu.Root>
      <Menu.Trigger asChild>
        <Button variant="ghost" aria-label={t('agents.roles.more', { name })} className="h-7 w-7 shrink-0 justify-center px-0">
          <Icon name="more" size={15} />
        </Button>
      </Menu.Trigger>
      <Menu.Content side="bottom" align="end" className="min-w-44">
        <Menu.Acts acts={[{ id: 'diagram', label: t('agents.roles.diagram.on'), onSelect: onDiagram }]} />
      </Menu.Content>
    </Menu.Root>
  );
}
