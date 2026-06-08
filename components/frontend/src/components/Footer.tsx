import { useTranslation } from 'react-i18next';
import { Code2, Flag, GraduationCap, Users } from 'lucide-react';

/** Team + tech, hardcoded proper nouns (not translated); labels around them are i18n'd. */
const TEAM = ['Christoph Himmler', 'Kamilo Knezevic', 'David Sommer', 'Stefan Wild'];
const STACK = ['React', '.NET', 'TimescaleDB', 'RabbitMQ', 'SignalR', 'MQTT'];

/**
 * App-wide footer (shown on every route incl. login): the academic context (FH JOANNEUM, the
 * ADSWE course), the team, the tech stack, and an F1 trademark disclaimer (the styling is a
 * tribute). Opaque carbon bar with an F1-red top accent so it reads over the racing background.
 */
export function Footer() {
    const { t } = useTranslation();

    return (
        <footer className="relative mt-auto border-t-2 border-f1-red bg-white/95 backdrop-blur dark:bg-slate-900/95">
            <div className="mx-auto grid w-full max-w-[120rem] gap-8 px-4 py-8 sm:px-6 md:grid-cols-4">
                <div>
                    <div className="flex items-center gap-2 text-lg font-semibold">
                        <Flag className="h-5 w-5 text-f1-red" aria-hidden="true" />
                        {t('app.title')}
                    </div>
                    <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                        {t('footer.tagline')}
                    </p>
                </div>

                <div>
                    <h3 className="flex items-center gap-2 text-xs font-semibold tracking-wide text-slate-700 uppercase dark:text-slate-300">
                        <GraduationCap className="h-4 w-4 text-f1-red" aria-hidden="true" />
                        {t('footer.courseHeading')}
                    </h3>
                    <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                        {t('footer.institution')}
                    </p>
                    <p className="text-sm text-slate-500 dark:text-slate-400">
                        {t('footer.course')}
                    </p>
                    <p className="text-sm text-slate-500 dark:text-slate-400">{t('footer.term')}</p>
                </div>

                <div>
                    <h3 className="flex items-center gap-2 text-xs font-semibold tracking-wide text-slate-700 uppercase dark:text-slate-300">
                        <Users className="h-4 w-4 text-f1-red" aria-hidden="true" />
                        {t('footer.teamHeading')}
                    </h3>
                    <ul className="mt-2 space-y-0.5">
                        {TEAM.map((member) => (
                            <li key={member} className="text-sm text-slate-500 dark:text-slate-400">
                                {member}
                            </li>
                        ))}
                    </ul>
                </div>

                <div>
                    <h3 className="flex items-center gap-2 text-xs font-semibold tracking-wide text-slate-700 uppercase dark:text-slate-300">
                        <Code2 className="h-4 w-4 text-f1-red" aria-hidden="true" />
                        {t('footer.builtWith')}
                    </h3>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                        {STACK.map((tech) => (
                            <span
                                key={tech}
                                className="rounded-full border border-slate-200 px-2 py-0.5 text-xs text-slate-600 dark:border-slate-700 dark:text-slate-300"
                            >
                                {tech}
                            </span>
                        ))}
                    </div>
                </div>
            </div>
        </footer>
    );
}
